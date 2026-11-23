global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using Stripe;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Localization;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;
using BonakMarket.StripeElements.Providers;
using BonakMarket.StripeElements.Settings;

namespace BonakMarket.StripeElements;

internal class Module : ModuleBase, IConfigurable, ICookiePublisher
{
    private readonly IPaymentService _paymentService;

    public Module(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    public Localizer T { get; set; } = NullLocalizer.Instance;

    public RouteInfo GetConfigurationRoute()
        => new("Configure", "StripeAdmin", new { area = "Admin" });

    public async Task<IEnumerable<CookieInfo>> GetCookieInfosAsync()
    {
        var store = Services.StoreContext.CurrentStore;
        var isActiveStripe = await _paymentService.IsPaymentProviderActiveAsync(StripeElementsProvider.SystemName, null, store.Id);

        if (isActiveStripe)
        {
            var cookieInfo = new CookieInfo
            {
                Name = T("Plugins.FriendlyName.BonakMarket.Stripe"),
                Description = T("Plugins.BonakMarket.Stripe.CookieInfo"),
                CookieType = CookieType.Required
            };

            return new List<CookieInfo> { cookieInfo }.AsEnumerable();
        }

        return null;
    }

    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await ImportLanguageResourcesAsync();
        await TrySaveSettingsAsync<StripeSettings>();

        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteLanguageResourcesAsync();
        await DeleteSettingsAsync<StripeSettings>();

        await base.UninstallAsync();
    }
}