using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped<CatalogFacade>();
        services.AddScoped<CatalogQueries>();
        services.AddScoped<OrderQueries>();
        services.AddScoped<CartFacade>();
        services.AddStorefrontSwagger();
    }

    public override void BuildPipeline(RequestPipelineBuilder builder)
    {
        if (!builder.ApplicationContext.IsInstalled) return;
        builder.Configure(StarterOrdering.BeforeRoutingMiddleware, app =>
            app.UseStorefrontSwagger(builder.ApplicationContext.HostEnvironment));
    }
}
