using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Messaging;
using BonakMarket.Identity.Configuration;

namespace BonakMarket.Identity.Filters;

/// <summary>
/// Selects the storefront's primary login method while keeping explicit password
/// sign-in and native password POST handling available.
/// </summary>
public sealed class SmsPrimaryLoginFilter : IAsyncActionFilter
{
    private readonly ISettingFactory _settings;
    private readonly ISmsDeliveryAvailability _delivery;

    public SmsPrimaryLoginFilter(ISettingFactory settings, ISmsDeliveryAvailability delivery)
    {
        _settings = settings;
        _delivery = delivery;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method) ||
            string.Equals(context.HttpContext.Request.Query["method"], "password", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var settings = await _settings.LoadSettingsAsync<CustomerIdentitySettings>();
        if (!settings.OtpLoginEnabled || !settings.SmsAsPrimaryLogin || !await _delivery.IsOtpTemplateAvailableAsync(settings.GetOtpTemplateKey("login"), context.HttpContext.RequestAborted))
        {
            await next();
            return;
        }

        var controller = (Controller)context.Controller;
        context.ActionArguments.TryGetValue("returnUrl", out var returnUrlArgument);
        context.ActionArguments.TryGetValue("checkoutAsGuest", out var checkoutAsGuest);
        string returnUrl = returnUrlArgument as string;

        context.Result = new RedirectToActionResult("Index", "SmsLogin", new
        {
            area = string.Empty,
            returnUrl = controller.Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            checkoutAsGuest = checkoutAsGuest is true
        });
    }
}
