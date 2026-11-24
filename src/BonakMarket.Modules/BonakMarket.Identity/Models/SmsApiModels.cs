#nullable enable

using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Identity.Models;

/// <summary>Requests a code for a mobile number.</summary>
public class SmsCodeRequest
{
    /// <summary>Gets or sets the Iranian mobile number in local or international format.</summary>
    [Required, StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(64)] public string? ReauthToken { get; set; }
}

/// <summary>Submits a single-use verification code.</summary>
public sealed class SmsCodeVerificationRequest : SmsCodeRequest
{
    /// <summary>Gets or sets the six-digit SMS code.</summary>
    [Required, RegularExpression("^[0-9]{6}$")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Opt in to mobile tokens without changing the existing web login response.</summary>
    public bool IssueTokens { get; set; }
}

public sealed class MobileRefreshRequest
{
    [Required, StringLength(128)]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class PhoneReauthRequest
{
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; set; } = string.Empty;
}
public sealed record PhoneReauthResponse(string ReauthToken, int ExpiresIn);
