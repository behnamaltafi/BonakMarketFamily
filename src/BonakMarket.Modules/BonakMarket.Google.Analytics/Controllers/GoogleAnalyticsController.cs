using Microsoft.AspNetCore.Mvc;
using BonakMarket.ComponentModel;
using BonakMarket.Core.Security;
using BonakMarket.Google.Analytics.Models;
using BonakMarket.Web.Controllers;
using BonakMarket.Web.Modelling.Settings;

namespace BonakMarket.Google.Analytics.Controllers;

[Area("Admin")]
public class GoogleAnalyticsController : ModuleController
{
    [LoadSetting, AuthorizeAdmin]
    public IActionResult Configure(GoogleAnalyticsSettings settings)
    {
        var model = MiniMapper.Map<GoogleAnalyticsSettings, ConfigurationModel>(settings);

        // If old script is used display hint to click on the 'Restore Scripts' button to get up-to-date script snippets.
        model.ScriptUpdateRecommended = settings.EcommerceScript.Contains("analytics.js");

        return View(model);
    }

    [HttpPost, SaveSetting, AuthorizeAdmin]
    public IActionResult Configure(ConfigurationModel model, GoogleAnalyticsSettings settings)
    {
        if (!ModelState.IsValid)
        {
            return Configure(settings);
        }

        ModelState.Clear();
        MiniMapper.Map(model, settings);

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));

        return RedirectToAction(nameof(Configure));
    }

    [HttpPost, SaveSetting, AuthorizeAdmin]
    [ActionName("Configure"), FormValueRequired("restore-scripts")]
    public IActionResult RestoreScripts(GoogleAnalyticsSettings settings)
    {
        settings.TrackingScript = AnalyticsScriptUtility.GetTrackingScript();
        settings.EcommerceScript = AnalyticsScriptUtility.GetEcommerceScript();
        settings.EcommerceDetailScript = AnalyticsScriptUtility.GetEcommerceDetailScript();

        return RedirectToAction(nameof(Configure));
    }
}