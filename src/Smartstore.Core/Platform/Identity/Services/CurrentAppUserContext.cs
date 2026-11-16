using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Data;

namespace Smartstore.Core.Identity;

/// <summary>
/// Resolves an AppUser from a trusted application cookie. During the
/// legacy Identity&lt;Customer&gt; bridge, the cookie must belong to the
/// explicitly associated login Customer, never the selected commercial buyer.
/// </summary>
public class CurrentAppUserContext : ICurrentAppUserContext
{
    public const string UserGuidClaim = "app_user_guid";
    public const string TrustedClaimIssuer = "Smartstore";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SmartDbContext _db;

    public CurrentAppUserContext(IHttpContextAccessor httpContextAccessor, SmartDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    private ClaimsIdentity GetTrustedCookieIdentity()
    {
        return _httpContextAccessor.HttpContext?.User?.Identities
            .FirstOrDefault(identity => identity.IsAuthenticated &&
                identity.AuthenticationType == IdentityConstants.ApplicationScheme &&
                identity.HasClaim(c => c.Type == UserGuidClaim && c.Issuer == TrustedClaimIssuer));
    }

    public bool IsAppUserIdentityPresent => GetTrustedCookieIdentity() is not null;

    public virtual async Task<AppUser> GetCurrentUserAsync(CancellationToken cancelToken = default)
    {
        var identity = GetTrustedCookieIdentity();
        if (identity is null)
            return null;

        var userClaims = identity.Claims
            .Where(c => c.Type == UserGuidClaim && c.Issuer == TrustedClaimIssuer)
            .ToArray();
        // ASP.NET Identity owns the NameIdentifier claim inside its signed
        // application cookie. Require exactly one matching legacy login ID.
        var loginClaims = identity.Claims
            .Where(c => c.Type == ClaimTypes.NameIdentifier)
            .ToArray();
        if (userClaims.Length != 1 || loginClaims.Length != 1 ||
            !Guid.TryParse(userClaims[0].Value, out var userGuid) || userGuid == Guid.Empty ||
            !int.TryParse(loginClaims[0].Value, out var legacyLoginId) || legacyLoginId <= 0)
            return null;

        // Revalidate the explicit bridge on each request so reassigned or
        // revoked mappings cannot reuse a stale AppUser identity cookie.
        var matches = await _db.AppUsers.AsNoTracking()
            .Where(x => x.UserGuid == userGuid && x.Active &&
                x.LegacyIdentityCustomerId == legacyLoginId)
            .Take(2)
            .ToArrayAsync(cancelToken);
        return matches.Length == 1 ? matches[0] : null;
    }
}
