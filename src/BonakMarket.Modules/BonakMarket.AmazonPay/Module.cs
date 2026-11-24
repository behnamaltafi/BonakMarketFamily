global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
global using Microsoft.Extensions.Logging;
global using BonakMarket.AmazonPay.Models;
global using BonakMarket.AmazonPay.Providers;
global using BonakMarket.Core.Localization;
global using BonakMarket.Web.Modelling;
using System.Linq;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Identity;
using BonakMarket.Engine.Modularity;
using BonakMarket.Scheduling;

namespace BonakMarket.AmazonPay;

internal class Module : ModuleBase, ICookiePublisher
{
    private readonly ITaskStore _taskStore;
    private readonly IPaymentService _paymentService;
    private readonly IProviderManager _providerManager;
    private readonly ExternalAuthenticationSettings _externalAuthenticationSettings;

    public Module(
        ITaskStore taskStore,
        IPaymentService paymentService,
        IProviderManager providerManager,
        ExternalAuthenticationSettings externalAuthenticationSettings)
    {
        _taskStore = taskStore;
        _paymentService = paymentService;
        _providerManager = providerManager;
        _externalAuthenticationSettings = externalAuthenticationSettings;
    }

    public Localizer T { get; set; } = NullLocalizer.Instance;

    public async Task<IEnumerable<CookieInfo>> GetCookieInfosAsync()
    {
        var store = Services.StoreContext.CurrentStore;
        var cookieInfoRequired = await _paymentService.IsPaymentProviderActiveAsync(AmazonPayProvider.SystemName, null, store.Id);

        if (!cookieInfoRequired)
        {
            var signInProvider = _providerManager.GetProvider<IExternalAuthenticationMethod>(AmazonPaySignInProvider.SystemName);
            cookieInfoRequired = signInProvider?.IsMethodActive(_externalAuthenticationSettings) ?? false;
        }

        if (cookieInfoRequired)
        {
            var cookieInfo = new CookieInfo
            {
                Name = T("Plugins.FriendlyName.Authentications.AmazonPay"),
                Description = T("Plugins.Payments.AmazonPay.CookieInfo"),
                CookieType = CookieType.Required
            };

            return new List<CookieInfo> { cookieInfo }.AsEnumerable();
        }

        return null;
    }

    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await TrySaveSettingsAsync<AmazonPaySettings>();
        await ImportLanguageResourcesAsync();

        // INFO: DataPollingTask has been removed. Its work was redundant.
        // It was intended for stores without SSL to update the payment status.
        // Payment status is updated by IPN, which requires SSL but SSL is required anyway.
        var pollingTask = await _taskStore.GetTaskByTypeAsync("BonakMarket.AmazonPay.DataPollingTask, BonakMarket.AmazonPay");
        if (pollingTask != null)
        {
            await _taskStore.DeleteTaskAsync(pollingTask);
        }

        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteSettingsAsync<AmazonPaySettings>();
        await DeleteLanguageResourcesAsync();

        await base.UninstallAsync();
    }
}