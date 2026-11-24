global using System;
global using System.Collections.Generic;
global using System.ComponentModel.DataAnnotations;
global using System.Linq;
global using System.Threading.Tasks;
global using FluentValidation;
global using Microsoft.EntityFrameworkCore;
global using BonakMarket.Core.Configuration;
global using BonakMarket.Engine.Modularity;
global using BonakMarket.Tax.Domain;
global using BonakMarket.Tax.Models;
global using BonakMarket.Web.Modelling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using BonakMarket.Http;

namespace BonakMarket.Tax;

internal class Module : ModuleBase, IConfigurable
{
    public ILogger Logger { get; set; } = NullLogger.Instance;

    public RouteInfo GetConfigurationRoute()
        => new("Providers", "Tax", new { area = "Admin" });

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