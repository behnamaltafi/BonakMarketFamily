using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Data;
using BonakMarket.Data.Providers;
using BonakMarket.StorefrontApi.Domain;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Swagger;

namespace BonakMarket.StorefrontApi;

internal sealed class Startup : StarterBase
{
    public override int Order => StarterOrdering.Late;

    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddScoped<StorefrontRequestContext>();
        services.AddScoped<ProductCatalogQueries>();
        services.AddScoped<ProductCatalogData>();
        services.AddScoped<ProductCatalogMapper>();
        services.AddScoped<CatalogFacade>();
        services.AddScoped<CatalogQueries>();
        services.AddScoped<OrderQueries>();
        services.AddScoped<CartFacade>();
        services.AddScoped<GuestCartService>();
        services.AddScoped<GuestCartSqlLock>();
        services.AddTransient<IDbContextConfigurationSource<SmartDbContext>, GuestCartModelConfigurer>();
        services.AddStorefrontSwagger();
    }

    public override void BuildPipeline(RequestPipelineBuilder builder)
    {
        if (!builder.ApplicationContext.IsInstalled) return;
        builder.Configure(StarterOrdering.BeforeRoutingMiddleware, app =>
            app.UseStorefrontSwagger(builder.ApplicationContext.HostEnvironment));
        builder.Configure(StarterOrdering.BeforeWorkContextMiddleware + 1, app =>
            app.UseMiddleware<GuestCartMiddleware>());
    }

    private sealed class GuestCartModelConfigurer : IDbContextConfigurationSource<SmartDbContext>
    {
        public void Configure(IServiceProvider services, DbContextOptionsBuilder builder) =>
            builder.UseDbFactory(factory => factory.AddModelAssembly(typeof(GuestCartToken).Assembly));
    }
}
