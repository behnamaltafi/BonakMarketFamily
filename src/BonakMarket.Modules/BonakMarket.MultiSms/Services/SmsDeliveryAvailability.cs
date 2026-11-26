using BonakMarket.Core.Configuration;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

internal sealed class SmsDeliveryAvailability(ISettingFactory settings) : ISmsDeliveryAvailability
{
    public async Task<bool> IsEnabledAsync() =>
        (await settings.LoadSettingsAsync<SmsGatewaySettings>()).Enabled;
}
