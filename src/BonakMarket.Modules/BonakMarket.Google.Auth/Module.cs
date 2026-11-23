global using System.Threading.Tasks;
global using Microsoft.AspNetCore.Authentication.Google;
global using Microsoft.Extensions.Options;
global using BonakMarket.Web.Modelling;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;
using BonakMarket.Google.Auth.Components;
using BonakMarket.Http;

namespace BonakMarket.Google.Auth;

internal class Module : ModuleBase, IConfigurable, IExternalAuthenticationMethod
{
    public RouteInfo GetConfigurationRoute()
        => new("Configure", "GoogleAuth", new { area = "Admin" });

    public Widget GetDisplayWidget(int storeId)
        => new ComponentWidget(typeof(GoogleAuthViewComponent), null);

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