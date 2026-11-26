using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

public sealed record SmsReadinessIssue(string Code, string Provider = null);
public sealed record SmsReadinessResult(bool IsReady, string[] ReadyProviders, SmsReadinessIssue[] Issues);

/// <summary>One local configuration check shared by administration, availability and actual sending.
/// A ready configuration is not proof of external provider acceptance.</summary>
public static class SmsReadinessService
{
    public static string ProviderIssue(ISmsProvider provider, SmsGatewaySettings settings, bool templateMode)
        => provider == null ? "UnknownProvider" : provider.GetConfigurationIssue(settings, templateMode);

    public static string TemplateProviderIssue(ISmsProvider provider, SmsGatewaySettings settings, SmsTemplateDefinition template)
    {
        var issue = ProviderIssue(provider, settings, true);
        if (issue != null) return issue;
        if (provider is not ITemplateSmsProvider transport) return "UnsupportedMode";
        if (provider.Name == "Mock") return null;
        if (!template.Providers.TryGetValue(provider.Name, out var mapping)) return "MissingMapping";
        if (!SmsTemplateRules.IsCompleteMapping(template.Parameters, mapping) ||
            !transport.IsValidMapping(mapping.ExternalTemplateId, mapping.Parameters.Values)) return "InvalidMapping";
        return null;
    }

    public static SmsReadinessResult Evaluate(SmsGatewaySettings settings, IEnumerable<ISmsProvider> providers,
        SmsTemplateDefinition template)
    {
        if (!settings.Enabled) return Failure("GatewayDisabled");
        if (!settings.DeliveryRoutingConfigured) return Failure("RoutingNotConfigured");
        if (template == null) return Failure("TemplateMissing");
        if (!template.IsActive) return Failure("TemplateInactive");
        if (!SmsTemplateRules.IsValidDefinition(template.Parameters, template.IsOtp)) return Failure("InvalidParameters");
        var route = SmsRouting.Providers(settings, true);
        if (route.Length == 0) return Failure("TemplateRouteMissing");
        var issues = new List<SmsReadinessIssue>();
        var ready = new List<string>();
        foreach (var name in route)
        {
            var provider = providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            var issue = TemplateProviderIssue(provider, settings, template);
            if (issue == null) ready.Add(name);
            else issues.Add(new SmsReadinessIssue(issue, name));
        }
        return new(ready.Count > 0, ready.ToArray(), issues.ToArray());
    }

    public static SmsReadinessResult EvaluateFreeText(SmsGatewaySettings settings, IEnumerable<ISmsProvider> providers)
    {
        if (!settings.Enabled) return Failure("GatewayDisabled");
        if (!settings.DeliveryRoutingConfigured) return Failure("RoutingNotConfigured");
        var route = SmsRouting.Providers(settings, false);
        if (route.Length == 0) return Failure("FreeTextRouteMissing");
        var issues = new List<SmsReadinessIssue>();
        var ready = new List<string>();
        foreach (var name in route)
        {
            var provider = providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            var issue = ProviderIssue(provider, settings, false);
            if (issue == null) ready.Add(name);
            else issues.Add(new(issue, name));
        }
        return new(ready.Count > 0, ready.ToArray(), issues.ToArray());
    }

    private static SmsReadinessResult Failure(string reason) => new(false, [], [new(reason)]);
}
