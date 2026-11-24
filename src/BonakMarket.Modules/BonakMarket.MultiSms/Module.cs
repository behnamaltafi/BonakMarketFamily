global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Net;
global using System.Net.Http;
global using System.Net.Http.Json;
global using System.Text.Json;
global using System.Threading;
global using System.Threading.Tasks;
global using BonakMarket.Web.Modelling;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms;

internal class Module : ModuleBase, IConfigurable
{
    public RouteInfo GetConfigurationRoute()
        => new("Configure", "MultiSms", new { area = "Admin" });

    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await TrySaveSettingsAsync<SmsGatewaySettings>();
        await ImportLanguageResourcesAsync();
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteSettingsAsync<SmsGatewaySettings>();
        await DeleteLanguageResourcesAsync();
        await base.UninstallAsync();
    }
}
