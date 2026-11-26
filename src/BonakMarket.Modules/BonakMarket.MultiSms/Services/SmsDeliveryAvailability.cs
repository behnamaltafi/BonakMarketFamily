using BonakMarket.Core.Configuration;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

internal sealed class SmsDeliveryAvailability(ISettingFactory settings, IEnumerable<ISmsProvider> providers,
    ISmsTemplateCatalog templates) : ISmsDeliveryAvailability
{
    public Task<bool> IsEnabledAsync()
        => IsOtpTemplateAvailableAsync(SmsTemplateRules.DefaultOtpKey);

    public async Task<bool> IsOtpTemplateAvailableAsync(string templateKey, CancellationToken ct = default)
    {
        var config = await settings.LoadSettingsAsync<SmsGatewaySettings>();
        if (!config.Enabled || !SmsTemplateRules.IsName(templateKey, 80)) return false;
        var route = SmsRouting.Providers(config, true);
        if (route.Length == 0) return false;
        var template = await templates.FindAsync(templateKey, ct);
        return IsReady(config, route, template);
    }

    public async Task<IReadOnlyList<SmsOtpTemplateOption>> GetOtpTemplatesAsync(CancellationToken ct = default)
    {
        var config = await settings.LoadSettingsAsync<SmsGatewaySettings>();
        var route = SmsRouting.Providers(config, true);
        var definitions = await templates.ListActiveOtpAsync(ct);
        return definitions.Where(IsValidOtp).Select(t => new SmsOtpTemplateOption(t.Key, t.Title ?? t.Key,
            config.Enabled && IsReady(config, route, t))).ToArray();
    }

    private static bool IsValidOtp(SmsTemplateDefinition template)
        => template is { IsActive: true, IsOtp: true } &&
           SmsTemplateRules.IsValidDefinition(template.Parameters, true);

    private bool IsReady(SmsGatewaySettings config, string[] route, SmsTemplateDefinition template)
    {
        if (!IsValidOtp(template)) return false;
        return route.Any(name => providers.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            && p is ITemplateSmsProvider transport && transport.IsTemplateAvailable(config)
            && (p.Name == "Mock" || (template.Providers.TryGetValue(p.Name, out var mapping)
                && SmsTemplateRules.IsCompleteMapping(template.Parameters, mapping)
                && transport.IsValidMapping(mapping.ExternalTemplateId, mapping.Parameters.Values)))));
    }
}
