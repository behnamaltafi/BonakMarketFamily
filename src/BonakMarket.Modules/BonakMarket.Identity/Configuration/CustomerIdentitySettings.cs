using BonakMarket.Core.Configuration;
namespace BonakMarket.Identity.Configuration;
public sealed class CustomerIdentitySettings : ISettings
{
    public const string DefaultOtpTemplateKey = "LOGINOTP";

    public string LoginOtpTemplateKey { get; set; } = DefaultOtpTemplateKey;
    public string PhoneEnrollmentOtpTemplateKey { get; set; } = DefaultOtpTemplateKey;
    public string ReauthenticationOtpTemplateKey { get; set; } = DefaultOtpTemplateKey;

    public static string EffectiveTemplateKey(string key)
        => string.IsNullOrWhiteSpace(key) ? DefaultOtpTemplateKey : key.Trim().ToUpperInvariant();

    public string GetOtpTemplateKey(string purpose)
    {
        if (purpose == "login") return EffectiveTemplateKey(LoginOtpTemplateKey);
        if (purpose?.StartsWith("enroll:", StringComparison.Ordinal) == true)
            return EffectiveTemplateKey(PhoneEnrollmentOtpTemplateKey);
        if (purpose?.StartsWith("reauth:", StringComparison.Ordinal) == true)
            return EffectiveTemplateKey(ReauthenticationOtpTemplateKey);
        throw new ArgumentException("Unknown OTP purpose.", nameof(purpose));
    }

    public int OtpResendIntervalSeconds { get; set; } = 60;
    public int EffectiveOtpResendIntervalSeconds() => Math.Clamp(OtpResendIntervalSeconds, 30, 300);
    public int OtpExpirationSeconds { get; set; } = 300;
    public int EffectiveOtpExpirationSeconds() => Math.Clamp(OtpExpirationSeconds, 60, 600);
    public int OtpMaxDailyRequests { get; set; } = 20;
    public int EffectiveOtpMaxDailyRequests() => Math.Clamp(OtpMaxDailyRequests, 1, 50);
    public int OtpMaxVerificationAttempts { get; set; } = 5;
    public int EffectiveOtpMaxVerificationAttempts() => Math.Clamp(OtpMaxVerificationAttempts, 1, 5);

    public bool OtpLoginEnabled { get; set; }
    public bool SmsAsPrimaryLogin { get; set; } = true;
}
