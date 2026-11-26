using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

/// <summary>Separate ordered routes. Mock must never be mixed with payable transports.</summary>
public static class SmsRouting
{
    public static string[] Providers(SmsGatewaySettings settings, bool templateMode)
    {
        if (!settings.DeliveryRoutingConfigured) return [];
        var primary = templateMode ? settings.TemplatePrimary : settings.FreeTextPrimary;
        if (string.IsNullOrWhiteSpace(primary)) return [];
        var failover = templateMode ? settings.TemplateFailover : settings.FreeTextFailover;
        var names = new[] { primary, templateMode ? settings.TemplateFallback1 : settings.FreeTextFallback1,
            templateMode ? settings.TemplateFallback2 : settings.FreeTextFallback2 };
        if (!failover) return [primary];
        var result = names.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (result.Length > 1 && result.Any(x => string.Equals(x, "Mock", StringComparison.OrdinalIgnoreCase)))
            return [];
        return result;
    }

    public static bool IsReady(ISmsProvider provider, SmsGatewaySettings settings, bool templateMode)
        => SmsReadinessService.ProviderIssue(provider, settings, templateMode) == null;
}
