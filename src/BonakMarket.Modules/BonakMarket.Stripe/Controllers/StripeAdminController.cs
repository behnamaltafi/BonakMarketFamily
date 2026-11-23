using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using BonakMarket.ComponentModel;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.Engine.Modularity;
using BonakMarket.StripeElements.Models;
using BonakMarket.StripeElements.Providers;
using BonakMarket.StripeElements.Services;
using BonakMarket.StripeElements.Settings;
using BonakMarket.Web.Controllers;
using BonakMarket.Web.Modelling.Settings;

namespace BonakMarket.StripeElements.Controllers;

[Area("Admin")]
public class StripeAdminController : ModuleController
{
    private readonly SmartDbContext _db;
    private readonly IProviderManager _providerManager;
    private readonly StripeHelper _stripeHelper;

    public StripeAdminController(
        SmartDbContext db,
        IProviderManager providerManager,
        StripeHelper stripeHelper)
    {
        _db = db;
        _providerManager = providerManager;
        _stripeHelper = stripeHelper;
    }

    [LoadSetting, AuthorizeAdmin]
    public IActionResult Configure(StripeSettings settings)
    {
        ViewBag.Provider = _providerManager.GetProvider(StripeElementsProvider.SystemName).Metadata;

        var model = MiniMapper.Map<StripeSettings, ConfigurationModel>(settings);

        model.WebhookUrl = Url.Action(nameof(StripeController.WebhookHandler), "Stripe", new { area = string.Empty }, "https");

        ViewBag.AvailableCaptureMethods = new List<SelectListItem>
        {
            new SelectListItem
            {
                Text = T("Plugins.BonakMarket.Stripe.CaptureMethod.Automatic"),
                Value = "automatic",
                Selected = "automatic" == settings.CaptureMethod
            },
            new SelectListItem
            {
                Text = T("Plugins.BonakMarket.Stripe.CaptureMethod.Manual"),
                Value = "manual",
                Selected = "manual" == settings.CaptureMethod
            }
        };

        ViewBag.AvailablePaymentPageButtonMethods = new List<SelectListItem>
        {
            new() { Text = "Apple Pay", Value = "applePay" },
            new() { Text = "Google Pay", Value = "googlePay" },
            new() { Text = "Link", Value = "link" },
            new() { Text = "PayPal", Value = "paypal" },
            new() { Text = "Amazon Pay", Value = "amazonPay" },
            new() { Text = "Klarna", Value = "klarna" }
        };

        ViewBag.CurrentCurrencyCode = Services.CurrencyService.PrimaryCurrency.CurrencyCode;

        return View(model);
    }

    [HttpPost, SaveSetting, AuthorizeAdmin]
    public IActionResult Configure(ConfigurationModel model, StripeSettings settings)
    {
        if (!ModelState.IsValid)
        {
            return Configure(settings);
        }

        ModelState.Clear();
        MiniMapper.Map(model, settings);
        settings.PaymentPageButtonMethods = model.PaymentPageButtonMethods ?? [];

        return RedirectToAction(nameof(Configure));
    }

    [HttpPost]
    [AuthorizeAdmin]
    [FormValueRequired("createwebhook"), ActionName("Configure")]
    public async Task<IActionResult> CreateWebhook(ConfigurationModel model)
    {
        var storeScope = GetActiveStoreScopeConfiguration();
        var settings = await Services.SettingFactory.LoadSettingsAsync<StripeSettings>(storeScope);

        if (settings.PublicApiKey.HasValue() && settings.SecrectApiKey.HasValue() && !settings.WebhookSecret.HasValue())
        {
            // Get Webhook ID vie API.
            try
            {
                // Get store URL
                var store = storeScope == 0 ? Services.StoreContext.CurrentStore : Services.StoreContext.GetStoreById(storeScope);
                var storeUrl = store.GetBaseUrl();

                settings.WebhookSecret = await _stripeHelper.GetWebHookIdAsync(settings.SecrectApiKey, storeUrl);
                await Services.SettingFactory.SaveSettingsAsync(settings, storeScope);
            }
            catch (Exception ex)
            {
                NotifyError(ex.Message);
            }
        }

        return RedirectToAction(nameof(Configure));
    }
}