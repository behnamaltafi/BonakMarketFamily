global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using BonakMarket.Web.Modelling;

using BonakMarket.Engine.Modularity;
using BonakMarket.Http;
using BonakMarket.Identity.Configuration;

namespace BonakMarket.Identity;

internal sealed class Module : ModuleBase, IConfigurable
{
    public RouteInfo GetConfigurationRoute()
        => new("Configure", "CustomerIdentityAdmin", new { area = "Admin" });

    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        // Copy the legacy global settings only when a new setting has not been saved.
        var legacy = new CustomerIdentitySettings
        {
            OtpLoginEnabled = await Services.Settings.GetSettingByKeyAsync("SmsGatewaySettings.OtpLoginEnabled", false),
            SmsAsPrimaryLogin = await Services.Settings.GetSettingByKeyAsync("SmsGatewaySettings.SmsAsPrimaryLogin", true)
        };
        await TrySaveSettingsAsync(legacy);
        await ImportLanguageResourcesAsync();
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        // Preserve settings, verified phone bindings and session data for reinstall.
        await DeleteLanguageResourcesAsync();
        await base.UninstallAsync();
    }
}
