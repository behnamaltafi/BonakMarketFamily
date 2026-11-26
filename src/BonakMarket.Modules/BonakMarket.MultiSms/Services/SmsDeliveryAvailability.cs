using BonakMarket.Core.Configuration;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

internal sealed class SmsDeliveryAvailability(ISettingFactory settings, IEnumerable<ISmsProvider> providers,
    ISmsTemplateCatalog templates) : ISmsDeliveryAvailability
{
    public async Task<bool> IsEnabledAsync()
    {
        var config = await settings.LoadSettingsAsync<SmsGatewaySettings>();
        if (!config.Enabled) return false;
        var route = SmsRouting.Providers(config, true);
        if (route.Length == 0) return false;
        var template = await templates.FindAsync(SmsTemplateRules.DefaultOtpKey, CancellationToken.None);
        if (template is null || !template.IsActive || !template.IsOtp ||
            !SmsTemplateRules.IsValidDefinition(template.Parameters, true)) return false;
        return route.Any(name => providers.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            && p is ITemplateSmsProvider transport && transport.IsTemplateAvailable(config)
            && (p.Name == "Mock" || (template.Providers.TryGetValue(p.Name, out var mapping)
                && SmsTemplateRules.IsCompleteMapping(template.Parameters, mapping)
                && transport.IsValidMapping(mapping.ExternalTemplateId, mapping.Parameters.Values)))));
    }
}
