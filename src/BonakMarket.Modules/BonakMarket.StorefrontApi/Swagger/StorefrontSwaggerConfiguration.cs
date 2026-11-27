using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BonakMarket.StorefrontApi.Swagger;

/// <summary>Commerce-only OpenAPI document and module-owned UI.</summary>
public static class StorefrontSwaggerConfiguration
{
    public const string DocumentName = "storefront";
    public const string DocumentTitle = "BonakMarket Storefront API";

    public static bool IsStorefrontPath(string path) =>
        path?.StartsWith("api/storefront/v1/", StringComparison.OrdinalIgnoreCase) == true;

    public static bool Includes(string document, ApiDescription api, Func<string, ApiDescription, bool> previous)
    {
        if (document == DocumentName)
            return api.GroupName == DocumentName && IsStorefrontPath(api.RelativePath);
        return api.GroupName != DocumentName && !IsStorefrontPath(api.RelativePath) && previous(document, api);
    }

    public static IServiceCollection AddStorefrontSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = DocumentTitle, Version = "v1",
                Description = "Customer-facing commerce API. Get a JWT access token from the Identity API (/docs/identity) and send it as Bearer authentication for customer operations. Catalog allows guest access subject to native permissions. Omit Authorization entirely for guest requests. Native browser cookies are not customer API credentials."
            });
            var previous = o.SwaggerGeneratorOptions.DocInclusionPredicate;
            o.DocInclusionPredicate((name, api) => Includes(name, api, previous));
            o.DocumentFilter<StorefrontDocumentFilter>();
            o.OperationFilter<StorefrontOperationFilter>();
        });
        return services;
    }

    public static void UseStorefrontSwagger(this IApplicationBuilder app, IHostEnvironment environment)
    {
        var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var enabled = environment.IsDevelopment() || config.GetValue<bool>("StorefrontApi:EnableSwagger");
        app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/docs/storefront"), branch =>
        {
            if (!enabled)
            {
                branch.Run(ctx => { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return Task.CompletedTask; });
                return;
            }

            branch.Use(async (ctx, next) =>
            {
                if (IsSwaggerFile(ctx.Request.Path) && !(IsDocumentPath(ctx.Request.Path)))
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await next();
            });
            branch.UseWhen(ctx => IsDocumentPath(ctx.Request.Path), docs =>
                docs.UseSwagger(o => o.RouteTemplate = "docs/storefront/{documentName}/swagger.json"));
            branch.UseSwaggerUI(o =>
            {
                o.RoutePrefix = "docs/storefront";
                o.SwaggerEndpoint("storefront/swagger.json", "Storefront API v1");
                o.EnableFilter();
                o.DisplayRequestDuration();
            });
        });
    }

    private static bool IsSwaggerFile(PathString path)
    {
        var value = path.Value?.TrimEnd('/') ?? "";
        return value.EndsWith("/swagger.json", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("/swagger.yaml", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDocumentPath(PathString path) =>
        string.Equals(path.Value?.TrimEnd('/'), "/docs/storefront/storefront/swagger.json", StringComparison.OrdinalIgnoreCase);
}
