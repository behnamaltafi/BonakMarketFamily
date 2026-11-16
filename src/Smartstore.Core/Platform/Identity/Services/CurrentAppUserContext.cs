using Microsoft.AspNetCore.Http;
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

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SmartDbContext _db;

    public CurrentAppUserContext(IHttpContextAccessor httpContextAccessor, SmartDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public virtual async Task<AppUser> GetCurrentUserAsync(CancellationToken cancelToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        var value = principal.FindFirst(UserGuidClaim)?.Value;
        if (!Guid.TryParse(value, out var userGuid) || userGuid == Guid.Empty)
            return null;

        return await _db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserGuid == userGuid && x.Active, cancelToken);
    }
}
