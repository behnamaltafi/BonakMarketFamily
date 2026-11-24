using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Domain;

namespace BonakMarket.Identity.Services;

/// <summary>JWT access tokens with SQL-backed, single-use refresh rotation.</summary>
internal sealed class MobileApiTokenService : IMobileApiTokenService
{
    private const string Issuer = "BonakMarket";
    private const string Audience = "BonakMarket.CustomerApi";
    private const int AccessSeconds = 600;
    private readonly SmartDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly UserManager<Customer> _users;
    private readonly SignInManager<Customer> _signIn;
    private readonly ISmsOtpLoginService _otp;

    public MobileApiTokenService(SmartDbContext db, IConfiguration configuration,
        UserManager<Customer> users, SignInManager<Customer> signIn, ISmsOtpLoginService otp)
    {
        _db = db;
        _configuration = configuration;
        _users = users;
        _signIn = signIn;
        _otp = otp;
    }

    private SymmetricSecurityKey SigningKey()
    {
        if (!bool.TryParse(_configuration["MobileApi:Enabled"], out var enabled) || !enabled)
            return null;
        try
        {
            var bytes = Convert.FromBase64String(_configuration["MobileApi:SigningKey"] ?? "");
            return bytes.Length >= 32 ? new SymmetricSecurityKey(bytes) : null;
        }
        catch (FormatException) { return null; }
    }

    public bool IsEnabled => SigningKey() != null;

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool Matches(string expected, string actual) =>
        expected != null && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));

    private static string Secret() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private async Task<Customer> EligibleAsync(int id, CancellationToken ct)
    {
        if (!IsEnabled || !await _otp.IsEnabledAsync()) return null;
        var customer = await _db.Customers.IncludeCustomerRoles().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (customer == null || customer.Deleted || !customer.Active || customer.IsSystemAccount ||
            !customer.IsRegistered() || customer.IsAdmin() || customer.IsSuperAdmin() ||
            !await _signIn.CanSignInAsync(customer) ||
            (_users.SupportsUserLockout && await _users.IsLockedOutAsync(customer)) ||
            (_users.SupportsUserTwoFactor && await _users.GetTwoFactorEnabledAsync(customer)))
            return null;
        return customer;
    }

    private Task<string> PhoneAsync(int id, CancellationToken ct) =>
        _db.Set<SmsLoginPhone>().AsNoTracking().Where(x => x.CustomerId == id)
            .Select(x => x.Phone).FirstOrDefaultAsync(ct);

    private async Task<string> StampAsync(Customer customer) =>
        _users.SupportsUserSecurityStamp ? await _users.GetSecurityStampAsync(customer) : null;

    private async Task<Customer> CheckSessionAsync(MobileApiSession session, CancellationToken ct)
    {
        if (session == null || session.RevokedOnUtc != null || session.ExpiresOnUtc <= DateTime.UtcNow)
            return null;
        var customer = await EligibleAsync(session.CustomerId, ct);
        if (customer == null || session.VerifiedPhone != await PhoneAsync(customer.Id, ct) ||
            session.SecurityStamp != await StampAsync(customer))
            return null;
        return customer;
    }

    private MobileApiTokenPair Pair(MobileApiSession session, string refreshSecret, SymmetricSecurityKey key)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddSeconds(AccessSeconds);
        if (expires > session.ExpiresOnUtc) expires = session.ExpiresOnUtc;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, session.CustomerId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Jti, session.AccessId.ToString("N")),
            new Claim("sid", session.SessionId.ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new Claim("token_use", "mobile_access")
        };
        var token = new JwtSecurityToken(Issuer, Audience, claims, now, expires,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new MobileApiTokenPair(new JwtSecurityTokenHandler().WriteToken(token),
            session.SessionId.ToString("N") + "." + refreshSecret,
            Math.Max(0, (int)(expires - now).TotalSeconds));
    }

    public async Task<MobileApiTokenPair> IssueAsync(Customer customer, CancellationToken ct)
    {
        var key = SigningKey();
        if (key == null || customer == null || await EligibleAsync(customer.Id, ct) == null) return null;
        var phone = await PhoneAsync(customer.Id, ct);
        if (string.IsNullOrEmpty(phone)) return null;
        var secret = Secret();
        var session = new MobileApiSession
        {
            SessionId = Guid.NewGuid(), CustomerId = customer.Id, AccessId = Guid.NewGuid(),
            RefreshHash = Hash(secret), VerifiedPhone = phone, SecurityStamp = await StampAsync(customer),
            ExpiresOnUtc = DateTime.UtcNow.AddDays(30)
        };
        _db.Set<MobileApiSession>().Add(session);
        await _db.SaveChangesAsync(ct);
        return Pair(session, secret, key);
    }

    public async Task<MobileApiTokenPair> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var key = SigningKey();
        if (key == null || string.IsNullOrEmpty(refreshToken) || refreshToken.Length > 128) return null;
        var pieces = refreshToken.Split('.');
        if (pieces.Length != 2 || !Guid.TryParseExact(pieces[0], "N", out var sid) || pieces[1].Length != 43)
            return null;
        var session = await _db.Set<MobileApiSession>().AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == sid, ct);
        if (await CheckSessionAsync(session, ct) == null) return null;
        var presented = Hash(pieces[1]);
        if (!Matches(session.RefreshHash, presented))
        {
            if (Matches(session.PreviousRefreshHash, presented))
            {
                var now = DateTime.UtcNow;
                await _db.Set<MobileApiSession>().Where(x => x.Id == session.Id && x.RevokedOnUtc == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedOnUtc, now), ct);
            }
            return null;
        }
        var next = Secret();
        var nextHash = Hash(next);
        var nextAccess = Guid.NewGuid();
        var utc = DateTime.UtcNow;
        var updated = await _db.Set<MobileApiSession>()
            .Where(x => x.Id == session.Id && x.RefreshHash == presented && x.RevokedOnUtc == null && x.ExpiresOnUtc > utc)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RefreshHash, nextHash)
                .SetProperty(x => x.PreviousRefreshHash, presented).SetProperty(x => x.AccessId, nextAccess), ct);
        if (updated != 1) return null;
        session.AccessId = nextAccess;
        return Pair(session, next, key);
    }

    public async Task<bool> RevokeAsync(string accessToken, CancellationToken ct)
    {
        var customer = await ValidateAsync(accessToken, ct);
        if (customer == null) return false;
        // The JWT has already passed full signature, lifetime and session validation.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        if (!Guid.TryParseExact(jwt.Claims.FirstOrDefault(x => x.Type == "sid")?.Value, "N", out var sid)) return false;
        var utc = DateTime.UtcNow;
        return await _db.Set<MobileApiSession>().Where(x => x.SessionId == sid &&
            x.CustomerId == customer.Id && x.RevokedOnUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedOnUtc, utc), ct) == 1;
    }

    public async Task<Customer> ValidateAsync(string accessToken, CancellationToken ct)
    {
        var key = SigningKey();
        if (key == null || string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 4096) return null;
        ClaimsPrincipal principal;
        try
        {
            principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(accessToken,
                new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = Issuer,
                    ValidateAudience = true, ValidAudience = Audience,
                    ValidateIssuerSigningKey = true, IssuerSigningKey = key,
                    RequireSignedTokens = true, ValidateLifetime = true, RequireExpirationTime = true,
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, ClockSkew = TimeSpan.Zero
                }, out _);
        }
        catch (SecurityTokenException) { return null; }
        catch (ArgumentException) { return null; }
        if (principal.FindFirst("token_use")?.Value != "mobile_access" ||
            !int.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var customerId) ||
            !Guid.TryParseExact(principal.FindFirst("sid")?.Value, "N", out var sid) ||
            !Guid.TryParseExact(principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, "N", out var jti))
            return null;
        var session = await _db.Set<MobileApiSession>().AsNoTracking().SingleOrDefaultAsync(
            x => x.SessionId == sid && x.CustomerId == customerId && x.AccessId == jti, ct);
        return await CheckSessionAsync(session, ct);
    }
}
