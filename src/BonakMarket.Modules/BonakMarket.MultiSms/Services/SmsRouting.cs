using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

/// <summary>Separate ordered routes. Mock must never be mixed with payable transports.</summary>
public static class SmsRouting
{
    public static string[] Providers(SmsGatewaySettings settings, bool otp)
    {
        if (!settings.RoutingConfigured) return [];
        var primary = otp ? settings.OtpPrimary : settings.GeneralPrimary;
        if (string.IsNullOrWhiteSpace(primary)) return [];
        var failover = otp ? settings.OtpFailover : settings.GeneralFailover;
        var names = new[] { primary, otp ? settings.OtpFallback1 : settings.GeneralFallback1,
            otp ? settings.OtpFallback2 : settings.GeneralFallback2 };
        if (!failover) return [primary];
        var result = names.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (result.Length > 1 && result.Any(x => string.Equals(x, "Mock", StringComparison.OrdinalIgnoreCase)))
            return [];
        return result;
    }

    public static bool IsReady(ISmsProvider provider, SmsGatewaySettings settings, bool otp)
        => otp ? provider is ITemplateSmsProvider dedicated && dedicated.IsTemplateAvailable(settings)
            : provider.IsAvailable(settings);
}
