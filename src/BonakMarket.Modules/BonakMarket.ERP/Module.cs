global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
using BonakMarket.Core;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;
using BonakMarket.Scheduling;
using BonakMarket.ERP.Tasks;

namespace BonakMarket.ERP;
internal sealed class Module : ModuleBase, IConfigurable
{
    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await ErpSynchronizationTask.EnsureRegisteredAsync(Services.Resolve<ITaskStore>());
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await Services.Resolve<ITaskStore>().TryDeleteTaskAsync<ErpSynchronizationTask>();
        await base.UninstallAsync();
    }

    public RouteInfo GetConfigurationRoute() => new("Index", "Erp", new { area = "Admin" });
}
