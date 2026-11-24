global using System.Threading.Tasks;
global using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
global using Microsoft.Extensions.Options;
global using BonakMarket.Web.Modelling;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;
using BonakMarket.Microsoft.Auth.Components;

namespace BonakMarket.Microsoft.Auth;

internal class Module : ModuleBase, IConfigurable, IExternalAuthenticationMethod
{
    public RouteInfo GetConfigurationRoute()
        => new("Configure", "MicrosoftAuth", new { area = "Admin" });

    public Widget GetDisplayWidget(int storeId)
        => new ComponentWidget(typeof(MicrosoftAuthViewComponent), null);

    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await ImportLanguageResourcesAsync();
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteLanguageResourcesAsync();
        await base.UninstallAsync();
    }
}