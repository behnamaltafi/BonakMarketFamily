using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Identity;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Identity.Filters;

namespace BonakMarket.Identity.Security;

// Run after native cookie authentication but before native WorkContext initialization.
public sealed class CustomerApiBearerMiddleware(RequestDelegate next)
{
    internal const string Scheme = "StorefrontBearer";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!UsesCustomerBearer(context.Request.Path) ||
            !context.Request.Headers.ContainsKey("Authorization"))
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        Customer customer = null;
        if (IdentityTransportPolicy.IsAllowed(context.Request) &&
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
            var problem = new ProblemDetails { Status = 401, Title = "invalid_access_token" };
            if (CustomerApiProblemFilter.IsCanonicalPath(context.Request.Path))
            {
                problem.Extensions["code"] = "invalid_access_token";
                problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
                await context.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions)null,
                    contentType: "application/problem+json", cancellationToken: context.RequestAborted);
            }
            else
                await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
            return;
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString()) }, Scheme);
        var principal = new ClaimsPrincipal(identity);
        context.User = principal;
        context.Features.Set<IAuthenticateResultFeature>(new AuthenticationFeature
        {
            AuthenticateResult = AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme))
        });
        context.Items[typeof(IMobileApiTokenService)] = customer;
        await next(context);
    }

    // Keep ERP/OData, MVC, SMS provider administration and anonymous OTP/refresh flows outside this scheme.
    public static bool UsesCustomerBearer(PathString path)
    {
        if (path.StartsWithSegments("/api/storefront/v1") || path.StartsWithSegments("/api/identity/v1/me"))
            return true;
        if (path.StartsWithSegments("/api/identity/v1/auth", out var auth))
        {
            var action = auth.Value?.TrimEnd('/') ?? "";
            return action.Equals("/session", StringComparison.OrdinalIgnoreCase)
                || action.Equals("/logout", StringComparison.OrdinalIgnoreCase);
        }
        if (!path.StartsWithSegments("/api/sms-login", out var remainder))
            return false;
        var endpoint = remainder.Value?.TrimEnd('/') ?? "";
        return !endpoint.Equals("/request", StringComparison.OrdinalIgnoreCase)
            && !endpoint.Equals("/verify", StringComparison.OrdinalIgnoreCase)
            && !endpoint.Equals("/refresh", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AuthenticationFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult AuthenticateResult { get; set; }
    }
}
