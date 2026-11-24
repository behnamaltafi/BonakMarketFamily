global using System;
global using System.ComponentModel.DataAnnotations;
global using System.Linq;
global using System.Threading.Tasks;
global using FluentValidation;
global using Microsoft.EntityFrameworkCore;
global using BonakMarket.Core.Configuration;
global using BonakMarket.Engine.Modularity;
global using BonakMarket.ShippingByWeight.Domain;
global using BonakMarket.ShippingByWeight.Models;
global using BonakMarket.Web.Modelling;
using BonakMarket.ShippingByWeight.Settings;

namespace BonakMarket.ShippingByWeight;

internal class Module : ModuleBase
{
    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await ImportLanguageResourcesAsync();
        await TrySaveSettingsAsync<ShippingByWeightSettings>();
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        // TODO: (mh) (core) Flush tables???
        await DeleteLanguageResourcesAsync();
        await DeleteSettingsAsync<ShippingByWeightSettings>();
        await base.UninstallAsync();
    }
}