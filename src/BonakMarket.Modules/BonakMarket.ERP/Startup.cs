using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Data;
using BonakMarket.Data;
using BonakMarket.Data.Providers;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.ERP.Contracts;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Providers.Varanegar;
using BonakMarket.ERP.Services;

namespace BonakMarket.ERP;
internal sealed class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddHttpClient("BonakMarket.ERP").ConfigurePrimaryHttpMessageHandler(
            () => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddScoped<IErpProvider, VaranegarProvider>();
        services.AddScoped<ErpProviderResolver>();
        services.AddScoped<ErpCatalogService>();
        services.AddSingleton<ErpSecrets>();
        services.AddTransient<IDbContextConfigurationSource<SmartDbContext>, ErpModelConfigurer>();
    }

    private sealed class ErpModelConfigurer : IDbContextConfigurationSource<SmartDbContext>
    {
        public void Configure(IServiceProvider services, DbContextOptionsBuilder builder)
            => builder.UseDbFactory(factory => factory.AddModelAssembly(typeof(ErpConnection).Assembly));
    }
}
