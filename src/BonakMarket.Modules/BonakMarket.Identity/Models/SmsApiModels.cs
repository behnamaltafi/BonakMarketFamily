#nullable enable

using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Identity.Models;

/// <summary>Requests a code for a mobile number.</summary>
public class SmsCodeRequest
{
    /// <summary>Gets or sets the Iranian mobile number in local or international format.</summary>
    [Required, StringLength(20)]
    public string Phone { get; set; } = string.Empty;
}

public sealed class CustomerLogoutRequest
{
    [StringLength(128)] public string? RefreshToken { get; set; }
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

public class PhoneChangeRequest : SmsCodeRequest
{
    [StringLength(64)] public string? ReauthToken { get; set; }
}
public sealed class PhoneChangeVerificationRequest : PhoneChangeRequest
{
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; set; } = string.Empty;
}
public sealed record PhoneChangeResponse(string Code, bool ReloginRequired);
