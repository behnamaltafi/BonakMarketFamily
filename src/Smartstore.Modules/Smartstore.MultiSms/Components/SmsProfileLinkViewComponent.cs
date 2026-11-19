using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Identity;
using Smartstore.MultiSms.Services;
using Smartstore.Web.Components;

namespace Smartstore.MultiSms.Components;

/// <summary>Shows the phone-linked profile editor on the native My Account page.</summary>
public sealed class SmsProfileLinkViewComponent : SmartViewComponent
{
    private readonly ISmsOtpLoginService _otp;
    private readonly UserManager<Customer> _userManager;
    private readonly SignInManager<Customer> _signInManager;

    public SmsProfileLinkViewComponent(ISmsOtpLoginService otp,
        UserManager<Customer> userManager, SignInManager<Customer> signInManager)
    {
        _otp = otp;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!await _otp.IsEnabledAsync() || !_signInManager.IsSignedIn(HttpContext.User))
        {
            return Empty();
        }

        var customer = await _userManager.GetUserAsync(HttpContext.User);
        if (customer is null || !customer.Active || customer.Deleted ||
            await _otp.GetVerifiedPhoneAsync(customer.Id) is null)
        {
            return Empty();
        }

        return View();
    }
}
