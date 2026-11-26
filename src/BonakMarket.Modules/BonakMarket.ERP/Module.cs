global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;

namespace BonakMarket.ERP;
internal sealed class Module : ModuleBase, IConfigurable
{
    public RouteInfo GetConfigurationRoute() => new("Index", "Erp", new { area = "Admin" });
}
