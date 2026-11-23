global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Net.Mime;
global using System.Threading.Tasks;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.OData.Deltas;
global using Microsoft.AspNetCore.OData.Results;
global using Microsoft.EntityFrameworkCore;
global using BonakMarket.Core.Common;
global using BonakMarket.Core.Data;
global using BonakMarket.Core.Security;
global using static Microsoft.AspNetCore.Http.StatusCodes;
global using MediaType = System.Net.Mime.MediaTypeNames.Application;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;

namespace BonakMarket.Web.Api;

internal class Module : ModuleBase, IConfigurable
{
    public static string SystemName => "BonakMarket.WebApi";

    public RouteInfo GetConfigurationRoute()
        => new("Configure", "WebApiAdmin", new { area = "Admin" });

    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await TrySaveSettingsAsync<WebApiSettings>();
        await ImportLanguageResourcesAsync();
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteSettingsAsync<WebApiSettings>();
        await DeleteLanguageResourcesAsync();
        await base.UninstallAsync();
    }
}