using Microsoft.AspNetCore.Mvc;
using BonakMarket.MultiSms.Services;
using BonakMarket.Web.Components;

namespace BonakMarket.MultiSms.Components;

/// <summary>
/// Adds the native OTP option to the existing BonakMarket login widget zone.
/// </summary>
public sealed class SmsLoginLinkViewComponent : SmartViewComponent
{
    private readonly SmsOtpLoginService _otp;

    public SmsLoginLinkViewComponent(SmsOtpLoginService otp)
    {
        _otp = otp;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!await _otp.IsEnabledAsync())
        {
            return Empty();
        }

        return View();
    }
}
