global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

using System.Runtime.CompilerServices;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;

[assembly: InternalsVisibleTo("BonakMarket.Core.Tests")]

namespace BonakMarket.StorefrontApi;

internal sealed class Module : ModuleBase, IConfigurable
{
    public RouteInfo GetConfigurationRoute()
        => new("Configure", "StorefrontApiAdmin", new { area = "Admin" });

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
