namespace BonakMarket.Identity.Models;

[LocalizedDisplay("Plugins.Identity.Customer.Fields.")]
public sealed class IdentityConfigurationModel : ModelBase
{
    [LocalizedDisplay("*OtpLoginEnabled")]
    public bool OtpLoginEnabled { get; set; }

    [LocalizedDisplay("*OtpMessageTemplate")]
    public string OtpMessageTemplate { get; set; }

    [LocalizedDisplay("*SmsAsPrimaryLogin")]
    public bool SmsAsPrimaryLogin { get; set; }
}
