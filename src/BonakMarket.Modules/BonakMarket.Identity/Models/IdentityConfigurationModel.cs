namespace BonakMarket.Identity.Models;

[LocalizedDisplay("Plugins.Identity.Customer.Fields.")]
public sealed class IdentityConfigurationModel : ModelBase
{
    [LocalizedDisplay("*OtpLoginEnabled")]
    public bool OtpLoginEnabled { get; set; }

    [LocalizedDisplay("*OtpMessageTemplate")]
    public string OtpMessageTemplate { get; set; }

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
