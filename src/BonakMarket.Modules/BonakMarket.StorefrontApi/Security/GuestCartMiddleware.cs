using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.StorefrontApi.Domain;

namespace BonakMarket.StorefrontApi.Security;

// After Identity bearer middleware, before WorkContext. Guest is never an authenticated principal.
public sealed class GuestCartMiddleware(RequestDelegate next)
{
    private static readonly PartitionedRateLimiter<HttpContext> CreationLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));

    public async Task InvokeAsync(HttpContext http)
    {
        if (!http.Request.Path.StartsWithSegments("/api/storefront/v1")) { await next(http); return; }
        var endpoint = http.GetEndpoint();
        var create = endpoint?.Metadata.GetMetadata<CreateGuestCartAttribute>() != null;
        var merge = endpoint?.Metadata.GetMetadata<MergeGuestCartAttribute>() != null;
        var guestAction = endpoint?.Metadata.GetMetadata<AllowGuestCartAttribute>() != null;
        var catalog = endpoint?.Metadata.GetMetadata<PublicCatalogAttribute>() != null;
        var bearer = http.Request.Headers.ContainsKey("Authorization");
        var header = http.Request.Headers.ContainsKey(GuestCartPolicy.Header);
        var verified = http.Items.TryGetValue(typeof(IMobileApiTokenService), out var v) ? v as Customer : null;
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        var enabled = GuestCartPolicy.Enabled(config);
        var cartMutation = verified != null && !HttpMethods.IsGet(http.Request.Method) &&
            !HttpMethods.IsHead(http.Request.Method) && !HttpMethods.IsOptions(http.Request.Method) &&
            (http.Request.Path.StartsWithSegments("/api/storefront/v1/cart") ||
             http.Request.Path.StartsWithSegments("/api/storefront/v1/wishlist") ||
             http.Request.Path.StartsWithSegments("/api/storefront/v1/checkout") ||
             http.Request.Path.StartsWithSegments("/api/storefront/v1/orders"));
        var wantsGuest = !bearer && (guestAction || (catalog && header));
        if (!create && !merge && !wantsGuest && !(enabled && cartMutation)) { await next(http); return; }
        if (!http.Request.IsHttps && config.GetValue("Identity:RequireHttps", true))
        { await Deny(http, 400, "https_required"); return; }
        if ((create || merge || wantsGuest) && !enabled)
        { await Deny(http, 403, "guest_cart_disabled"); return; }
        if (create)
        {
            if (bearer || header) { await Deny(http, 409, "guest_cart_creation_requires_anonymous"); return; }
            using var lease = CreationLimiter.AttemptAcquire(http);
            var overload = http.RequestServices.GetRequiredService<IOverloadProtector>();
            if (!lease.IsAcquired || await overload.ForbidNewGuestAsync(http) || await overload.DenyGuestAsync(http))
            {
                http.Response.Headers["Retry-After"] = "60";
                await Deny(http, 429, "guest_cart_rate_limited"); return;
            }
            await next(http); return;
        }
        if (merge && verified == null) { await Deny(http, 401, "authentication_required"); return; }
        var hash = (merge || wantsGuest) ? GuestCartPolicy.ReadHash(http.Request) : null;
        if ((merge || wantsGuest) && hash == null)
        { await Deny(http, 401, header ? "invalid_guest_cart_token" : "guest_cart_token_required"); return; }
        var locks = http.RequestServices.GetRequiredService<GuestCartSqlLock>();
        if (!locks.Supported)
        {
            if (merge || wantsGuest) { await Deny(http, 503, "guest_cart_unavailable"); return; }
            await next(http); return;
        }
        IAsyncDisposable customerLease = null, guestLease = null;
        try
        {
            if (cartMutation) customerLease = await locks.AcquireAsync(GuestCartSqlLock.CustomerKey(verified.Id), http.RequestAborted);
            if (hash != null) guestLease = await locks.AcquireAsync(GuestCartSqlLock.GuestKey(hash), http.RequestAborted);
        }
        catch (TimeoutException)
        {
            if (guestLease != null) await guestLease.DisposeAsync();
            if (customerLease != null) await customerLease.DisposeAsync();
            await Deny(http, 409, "cart_busy"); return;
        }
        catch
        {
            if (guestLease != null) await guestLease.DisposeAsync();
            if (customerLease != null) await customerLease.DisposeAsync();
            throw;
        }
        await using var customerHandle = customerLease;
        await using var guestHandle = guestLease;
        if (wantsGuest)
        {
            var db = http.RequestServices.GetRequiredService<SmartDbContext>();
            var store = http.RequestServices.GetRequiredService<IStoreContext>().CurrentStore.Id;
            var token = await db.Set<GuestCartToken>().AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == hash &&
                x.StoreId == store && x.RevokedOnUtc == null && x.MergedOnUtc == null && x.ExpiresOnUtc > DateTime.UtcNow, http.RequestAborted);
            var customer = token == null ? null : await db.Customers.IncludeCustomerRoles()
                .FirstOrDefaultAsync(x => x.Id == token.CustomerId, http.RequestAborted);
            if (!GuestCartPolicy.IsGuest(customer)) { await Deny(http, 401, "invalid_guest_cart_token"); return; }
            if (await http.RequestServices.GetRequiredService<IOverloadProtector>().DenyGuestAsync(http, customer))
            { await Deny(http, 429, "guest_cart_rate_limited"); return; }
            http.RequestServices.GetRequiredService<IWorkContext>().CurrentCustomer = customer;
            http.Items[typeof(GuestCartToken)] = token;
        }
        await next(http);
    }

    private static async Task Deny(HttpContext http, int status, string code)
    {
        http.Response.StatusCode = status;
        http.Response.Headers.CacheControl = "no-store";
        var problem = new ProblemDetails { Status = status, Title = code };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? http.TraceIdentifier;
        await http.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions)null,
            contentType: "application/problem+json", cancellationToken: http.RequestAborted);
    }
}
