using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BonakMarket.Identity.Security;

/// <summary>HTTPS is required unless explicitly disabled for development/testing.</summary>
internal static class IdentityTransportPolicy
{
    public static bool IsAllowed(HttpRequest request)
    {
        if (request.IsHttps) return true;

        var configuration = request.HttpContext.RequestServices?.GetService<IConfiguration>();
        return bool.TryParse(configuration?["Identity:RequireHttps"], out var requireHttps) && !requireHttps;
    }
}
