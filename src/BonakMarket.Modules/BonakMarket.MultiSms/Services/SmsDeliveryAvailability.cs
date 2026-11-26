using System.Threading;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

internal sealed class SmsDeliveryAvailability(ISettingFactory settings, IEnumerable<ISmsProvider> providers,
    ISmsTemplateCatalog templates) : ISmsDeliveryAvailability
{
    public Task<bool> IsEnabledAsync() => IsOtpTemplateAvailableAsync(SmsTemplateRules.DefaultOtpKey);

    public async Task<bool> IsOtpTemplateAvailableAsync(string templateKey, CancellationToken ct = default)
    {
        var config = await settings.LoadSettingsAsync<SmsGatewaySettings>();
        if (!config.Enabled || !SmsTemplateRules.IsName(templateKey, 80)) return false;
        var template = await templates.FindAsync(templateKey, ct);
        return template is { IsOtp: true } && SmsReadinessService.Evaluate(config, providers, template).IsReady;
    }

    public async Task<IReadOnlyList<SmsOtpTemplateOption>> GetOtpTemplatesAsync(CancellationToken ct = default)
    {
        var config = await settings.LoadSettingsAsync<SmsGatewaySettings>();
        var definitions = await templates.ListActiveOtpAsync(ct);
        return definitions.Where(t => t.IsOtp && t.IsActive && SmsTemplateRules.IsValidDefinition(t.Parameters, true))
            .Select(t =>
            {
                var status = SmsReadinessService.Evaluate(config, providers, t);
                return new SmsOtpTemplateOption(t.Key, t.Title ?? t.Key, status.IsReady,
                    status.Issues.Select(x => new SmsOtpTemplateIssue(x.Code, x.Provider)).ToArray(), status.ReadyProviders);
            }).ToArray();
    }
}
