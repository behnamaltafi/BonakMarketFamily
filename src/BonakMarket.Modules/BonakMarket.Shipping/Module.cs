global using System;
global using System.ComponentModel.DataAnnotations;
global using System.Linq;
global using System.Threading.Tasks;
global using FluentValidation;
global using Microsoft.EntityFrameworkCore;
global using BonakMarket.Core.Configuration;
global using BonakMarket.Engine.Modularity;
global using BonakMarket.Shipping.Domain;
global using BonakMarket.Shipping.Models;
global using BonakMarket.Web.Modelling;
using BonakMarket.Shipping.Settings;

namespace BonakMarket.Shipping;

internal class Module : ModuleBase
{
    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await SaveSettingsAsync<ShippingByTotalSettings>();
        await ImportLanguageResourcesAsync();
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteSettingsAsync<ShippingByTotalSettings>();
        await DeleteLanguageResourcesAsync();
        await base.UninstallAsync();
    }
}