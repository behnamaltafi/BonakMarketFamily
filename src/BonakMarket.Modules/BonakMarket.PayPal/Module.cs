global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Threading.Tasks;
global using BonakMarket.Core.Checkout.Payment;
global using BonakMarket.Core.Localization;
global using BonakMarket.PayPal.Models;
global using BonakMarket.PayPal.Settings;
global using BonakMarket.Web.Modelling;
using BonakMarket.Engine.Modularity;

namespace BonakMarket.PayPal;

internal class Module : ModuleBase
{
    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await ImportLanguageResourcesAsync();
        await TrySaveSettingsAsync<PayPalSettings>();
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteLanguageResourcesAsync();
        await DeleteSettingsAsync<PayPalSettings>();
        await base.UninstallAsync();
    }
}