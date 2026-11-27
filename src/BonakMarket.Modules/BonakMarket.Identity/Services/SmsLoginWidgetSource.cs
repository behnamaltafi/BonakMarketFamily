using BonakMarket.Core.Widgets;
using BonakMarket.Identity.Components;

namespace BonakMarket.Identity.Services;

/// <summary>
/// Adds SMS sign-in to the native login page whenever OTP is enabled,
/// without requiring separate CMS widget activation.
/// </summary>
public sealed class SmsLoginWidgetSource : IWidgetSource
{
    private readonly Lazy<ISmsOtpLoginService> _otp;

    public SmsLoginWidgetSource(Lazy<ISmsOtpLoginService> otp)
    {
        _otp = otp;
    }

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public async Task<IEnumerable<Widget>> GetWidgetsAsync(IWidgetZone zone, bool isPublicArea, object model = null)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (!isPublicArea ||
            (!string.Equals(zone.Name, "after_login_buttons", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(zone.Name, "customer_info_top", StringComparison.OrdinalIgnoreCase)) ||
            !(string.Equals(zone.Name, "customer_info_top", StringComparison.OrdinalIgnoreCase)
                ? await _otp.Value.IsEnrollmentEnabledAsync()
                : await _otp.Value.IsEnabledAsync()))
        {
            return Array.Empty<Widget>();
        }

        return string.Equals(zone.Name, "customer_info_top", StringComparison.OrdinalIgnoreCase)
            ? [new ComponentWidget(typeof(SmsProfileLinkViewComponent)) { Key = "BonakMarket.MultiSms.ProfileLink" }]
            : [new ComponentWidget(typeof(SmsLoginLinkViewComponent)) { Key = "BonakMarket.MultiSms.LoginLink" }];
    }
}
