using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Identity;
using Common.DTO;

namespace BonakMarket.StorefrontApi.Security;

// Run after native cookie authentication but before native WorkContext initialization.
public sealed class StorefrontBearerMiddleware(RequestDelegate next)
{
    internal static readonly object CustomerKey = new();
    internal const string Scheme = "StorefrontBearer";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/storefront/v1") ||
            !context.Request.Headers.ContainsKey("Authorization"))
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        Customer customer = null;
        if (context.Request.IsHttps &&
            AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization.ToString(), out var header) &&
            header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(header.Parameter))
        {
            var tokens = context.RequestServices.GetService<IMobileApiTokenService>();
            if (tokens?.IsEnabled == true)
                customer = await tokens.ValidateAsync(header.Parameter, context.RequestAborted);
        }
        if (customer == null)
        {
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new ApiResult("invalid_access_token", false, null, 401), context.RequestAborted);
            return;
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()) }, Scheme);
        var principal = new ClaimsPrincipal(identity);
        context.User = principal;
        context.Features.Set<IAuthenticateResultFeature>(new AuthenticationFeature
        {
            AuthenticateResult = AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme))
        });
        context.Items[CustomerKey] = customer;
        await next(context);
    }

    private sealed class AuthenticationFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult AuthenticateResult { get; set; }
    }
}
