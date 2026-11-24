using BonakMarket.Core.Configuration;
namespace BonakMarket.Identity.Configuration;
public sealed class CustomerIdentitySettings : ISettings
{
    public bool OtpLoginEnabled { get; set; }
    public bool SmsAsPrimaryLogin { get; set; } = true;
}
