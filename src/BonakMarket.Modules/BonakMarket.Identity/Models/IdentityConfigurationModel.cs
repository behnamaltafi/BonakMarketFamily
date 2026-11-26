using BonakMarket.Core.Messaging;
using BonakMarket.Identity.Configuration;

namespace BonakMarket.Identity.Models;

[LocalizedDisplay("Plugins.Identity.Customer.Fields.")]
public sealed class IdentityConfigurationModel : ModelBase
{
    [LocalizedDisplay("*OtpLoginEnabled")]
    public bool OtpLoginEnabled { get; set; }

    [LocalizedDisplay("*LoginOtpTemplateKey")]
    public string LoginOtpTemplateKey { get; set; } = CustomerIdentitySettings.DefaultOtpTemplateKey;

    [LocalizedDisplay("*PhoneEnrollmentOtpTemplateKey")]
    public string PhoneEnrollmentOtpTemplateKey { get; set; } = CustomerIdentitySettings.DefaultOtpTemplateKey;

    [LocalizedDisplay("*ReauthenticationOtpTemplateKey")]
    public string ReauthenticationOtpTemplateKey { get; set; } = CustomerIdentitySettings.DefaultOtpTemplateKey;

    public IReadOnlyList<SmsOtpTemplateOption> OtpTemplates { get; set; } = Array.Empty<SmsOtpTemplateOption>();

    [LocalizedDisplay("*OtpResendIntervalSeconds")]
    [System.ComponentModel.DataAnnotations.Range(30, 300)]
    public int OtpResendIntervalSeconds { get; set; } = 60;

    [LocalizedDisplay("*OtpExpirationSeconds")]
    [System.ComponentModel.DataAnnotations.Range(60, 600)]
    public int OtpExpirationSeconds { get; set; } = 300;

    [LocalizedDisplay("*OtpMaxDailyRequests")]
    [System.ComponentModel.DataAnnotations.Range(1, 50)]
    public int OtpMaxDailyRequests { get; set; } = 20;

    [LocalizedDisplay("*OtpMaxVerificationAttempts")]
    [System.ComponentModel.DataAnnotations.Range(1, 5)]
    public int OtpMaxVerificationAttempts { get; set; } = 5;

    [LocalizedDisplay("*SmsAsPrimaryLogin")]
    public bool SmsAsPrimaryLogin { get; set; }
}
