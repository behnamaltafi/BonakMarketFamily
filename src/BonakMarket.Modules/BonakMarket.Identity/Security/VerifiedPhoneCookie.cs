using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Domain;

namespace BonakMarket.Identity.Security;

// Native UserStore does not implement IUserSecurityStampStore. Bind customer cookies to the verified-phone version.
internal static class VerifiedPhoneCookie
{
    private const string ClaimName = "bonakmarket:verified_phone_version";

    public static void Configure(CookieAuthenticationOptions options)
    {
        var previousSignIn = options.Events.OnSigningIn;
        var previousValidate = options.Events.OnValidatePrincipal;
        options.Events.OnSigningIn = async context =>
        {
            await previousSignIn(context);
            var value = await ReadAsync(context.HttpContext.RequestServices, context.Principal);
            if (value != null) Set(context.Principal, value);
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var value = await ReadAsync(context.HttpContext.RequestServices, context.Principal);
            var actual = context.Principal?.FindFirst(ClaimName)?.Value;
            if (value != actual)
            {
                context.RejectPrincipal();
                return;
            }
            await previousValidate(context);
            // Native validation may rebuild a principal; retain the checked claim.
            if (value != null && context.Principal != null) Set(context.Principal, value);
        };
    }

    private static void Set(ClaimsPrincipal principal, string value)
    {
        if (principal?.Identity is not ClaimsIdentity identity) return;
        foreach (var claim in identity.FindAll(ClaimName).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(ClaimName, value));
    }

    private static async Task<string> ReadAsync(IServiceProvider services, ClaimsPrincipal principal)
    {
        var manager = services.GetRequiredService<UserManager<Customer>>();
        if (principal == null || !int.TryParse(manager.GetUserId(principal), out var id)) return null;
        var db = services.GetRequiredService<SmartDbContext>();
        var binding = await db.Set<SmsLoginPhone>().AsNoTracking().Where(x => x.CustomerId == id)
            .Select(x => new { x.Phone, x.VerifiedOnUtc }).FirstOrDefaultAsync();
        return binding == null ? null : binding.Phone + ":" + binding.VerifiedOnUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
