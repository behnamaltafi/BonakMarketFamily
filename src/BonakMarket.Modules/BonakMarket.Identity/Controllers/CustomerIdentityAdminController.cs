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
        SmsAsPrimaryLogin = settings.SmsAsPrimaryLogin
    });

    [HttpPost, SaveSetting(false)]
    public IActionResult Configure(IdentityConfigurationModel model, CustomerIdentitySettings settings)
    {
        if (!ModelState.IsValid) return View(model);
        settings.OtpLoginEnabled = model.OtpLoginEnabled;
        settings.SmsAsPrimaryLogin = model.SmsAsPrimaryLogin;
        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
        return RedirectToAction(nameof(Configure));
    }
}
