using BonakMarket.Core.Configuration;
namespace BonakMarket.Identity.Configuration;
public sealed class CustomerIdentitySettings : ISettings
{
    public const string CodePlaceholder = "{Code}";
    public const string DefaultOtpMessageTemplate =
        "بنک مارکت\n" +
        "«محرمانه»\n" +
        "از در اختیار قرار دادن محتوای این پیامک به دیگران، اکیدا خودداری کنید.\n" +
        "کد احراز هویت جهت ورود:\n" +
        CodePlaceholder;

    public string OtpMessageTemplate { get; set; } = DefaultOtpMessageTemplate;

    public static bool IsValidOtpMessageTemplate(string template)
        => !string.IsNullOrWhiteSpace(template) && template.Contains(CodePlaceholder, StringComparison.Ordinal);

    public string FormatOtpMessage(string code)
        => (IsValidOtpMessageTemplate(OtpMessageTemplate) ? OtpMessageTemplate : DefaultOtpMessageTemplate)
            .Replace(CodePlaceholder, code, StringComparison.Ordinal);

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
