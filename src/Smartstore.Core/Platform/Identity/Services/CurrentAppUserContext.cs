using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Data;

namespace Smartstore.Core.Identity;

/// <summary>
/// Resolves the app_user_guid claim issued by trusted authentication infrastructure.
/// Never substitutes Customer.Id or an unverified phone/email.
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

    private Claim GetTrustedAppUserClaim()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        return principal?.Identities
            .Where(identity => identity.IsAuthenticated &&
                identity.AuthenticationType == IdentityConstants.ApplicationScheme)
            .SelectMany(identity => identity.Claims)
            .FirstOrDefault(claim => claim.Type == UserGuidClaim &&
                claim.Issuer == TrustedClaimIssuer);
    }

    public bool IsAppUserIdentityPresent => GetTrustedAppUserClaim() is not null;

    public virtual async Task<AppUser> GetCurrentUserAsync(CancellationToken cancelToken = default)
    {
        // Claims from an external provider principal are not an AppUser sign-in.
        var value = GetTrustedAppUserClaim()?.Value;
        if (!Guid.TryParse(value, out var userGuid) || userGuid == Guid.Empty)
            return null;

        return await _db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserGuid == userGuid && x.Active, cancelToken);
    }
}
