using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;
using BonakMarket.StorefrontApi.Queries;
using Swashbuckle.AspNetCore.SwaggerGen;

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
        // Register a separate document; never change OData routes or authentication.
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("storefront", new OpenApiInfo { Title = "BonakMarket Storefront API", Version = "v1",
                Description = "Customer API. Identity module: use /api/identity/v1/auth/session, auth/sms/request, auth/sms/verify (issueTokens=true) and auth/refresh for mobile authentication. Cookie mutations require the antiforgery header from session." });
            var existing = o.SwaggerGeneratorOptions.DocInclusionPredicate;
            o.DocInclusionPredicate((name, api) => name == "storefront"
                ? api.GroupName == "storefront" || (api.RelativePath?.StartsWith("api/identity/v1/", StringComparison.OrdinalIgnoreCase) == true)
                : api.RelativePath?.StartsWith("api/identity/v1/", StringComparison.OrdinalIgnoreCase) != true &&
                  api.RelativePath?.StartsWith("api/sms-login/", StringComparison.OrdinalIgnoreCase) != true && existing(name, api));
            o.DocumentFilter<StorefrontDocumentFilter>();
        });
    }

    public override void BuildPipeline(RequestPipelineBuilder builder)
    {
        if (!builder.ApplicationContext.IsInstalled) return;

        builder.Configure(StarterOrdering.BeforeRoutingMiddleware, app =>
        {
            var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
            if (!builder.ApplicationContext.HostEnvironment.IsDevelopment() &&
                !config.GetValue<bool>("StorefrontApi:EnableSwagger")) return;

            app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/docs/storefront"), branch =>
            {
                // Only expose the storefront JSON, not the other documents on this prefix.
                branch.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.Value?.EndsWith("/swagger.json", StringComparison.OrdinalIgnoreCase) == true &&
                        !string.Equals(ctx.Request.Path.Value, "/docs/storefront/storefront/swagger.json", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = 404;
                        return;
                    }
                    await next();
                });
                branch.UseSwagger(o => o.RouteTemplate = "docs/storefront/{documentName}/swagger.json");
                branch.UseSwaggerUI(o =>
                {
                    o.RoutePrefix = "docs/storefront";
                    o.SwaggerEndpoint("storefront/swagger.json", "Storefront v1");
                });
            });
        });
    }
}
