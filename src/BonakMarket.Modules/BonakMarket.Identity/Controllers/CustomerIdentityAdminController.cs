using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Security;
using BonakMarket.Identity.Configuration;
using BonakMarket.Identity.Models;
using BonakMarket.Web.Controllers;
using BonakMarket.Web.Modelling.Settings;

namespace BonakMarket.Identity.Controllers;

[Permission("configuration.setting")]
public sealed class CustomerIdentityAdminController : AdminController
{
    [LoadSetting(false)]
    public IActionResult Configure(CustomerIdentitySettings settings) => View(new IdentityConfigurationModel
    {
        OtpLoginEnabled = settings.OtpLoginEnabled,
        OtpMessageTemplate = CustomerIdentitySettings.IsValidOtpMessageTemplate(settings.OtpMessageTemplate)
            ? settings.OtpMessageTemplate : CustomerIdentitySettings.DefaultOtpMessageTemplate,
        SmsAsPrimaryLogin = settings.SmsAsPrimaryLogin
    });

    [HttpPost, SaveSetting(false)]
    public IActionResult Configure(IdentityConfigurationModel model, CustomerIdentitySettings settings)
    {
        if (!CustomerIdentitySettings.IsValidOtpMessageTemplate(model.OtpMessageTemplate))
        {
            ModelState.AddModelError(nameof(model.OtpMessageTemplate), T("Plugins.Identity.Customer.Fields.OtpMessageTemplate.Invalid").Value);
        }

        if (!ModelState.IsValid) return View(model);
        settings.OtpMessageTemplate = model.OtpMessageTemplate;
        settings.OtpLoginEnabled = model.OtpLoginEnabled;
        settings.SmsAsPrimaryLogin = model.SmsAsPrimaryLogin;
        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
        return RedirectToAction(nameof(Configure));
    }
}
