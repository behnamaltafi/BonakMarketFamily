#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Identity.Models;

public sealed class MobileSmsVerifyRequest
{
    [Required, StringLength(1024)] public string ChallengeId { get; set; } = string.Empty;
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; set; } = string.Empty;
}

public sealed record MobileSmsChallengeResponse(string ChallengeId, int ExpiresIn, int ResendAfter, string MaskedPhone);
public sealed record MobileSmsSignInResponse(string AccessToken, string RefreshToken, string TokenType,
    int ExpiresIn, bool ProfileRequired, string[] MissingRequiredFields);
