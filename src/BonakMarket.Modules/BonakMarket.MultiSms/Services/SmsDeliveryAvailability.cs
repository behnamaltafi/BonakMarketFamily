using BonakMarket.Core.Configuration;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

internal sealed class SmsDeliveryAvailability(ISettingFactory settings, IEnumerable<ISmsProvider> providers) : ISmsDeliveryAvailability
{
    public async Task<bool> IsEnabledAsync()
    {
        var config = await settings.LoadSettingsAsync<SmsGatewaySettings>();
        if (!config.Enabled) return false;
        var route = SmsRouting.Providers(config, true);
        return route.Any(name => providers.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            && SmsRouting.IsReady(p, config, true)));
    }
}
