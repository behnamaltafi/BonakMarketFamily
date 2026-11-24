using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BonakMarket.Engine.Modularity;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BonakMarket.Identity.Swagger;

/// <summary>Identity owns its document and the optional customer documentation portal.
/// The portal discovers installed modules; it does not reference the commerce assembly.</summary>
public static class IdentitySwaggerConfiguration
{
    public const string DocumentName = "identity";
    public const string DocumentTitle = "BonakMarket Identity API";

    public static bool IsIdentityPath(string path) =>
        path?.StartsWith("api/identity/v1/", StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsLegacyPath(string path) =>
        path?.StartsWith("api/sms-login/", StringComparison.OrdinalIgnoreCase) == true;

    public static bool Includes(string document, ApiDescription api, Func<string, ApiDescription, bool> previous)
    {
        if (document == DocumentName)
            return api.GroupName == DocumentName && IsIdentityPath(api.RelativePath);
        return api.GroupName != DocumentName && !IsIdentityPath(api.RelativePath) &&
            !IsLegacyPath(api.RelativePath) && previous(document, api);
    }

    public static IServiceCollection AddIdentitySwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = DocumentTitle, Version = "v1",
                Description = "Customer sign-in, session, profile and account security. Mobile: POST auth/mobile/sms/request then auth/mobile/sms/verify; no cookie or CSRF required. Browser: GET auth/session, retain the antiforgery cookie and headerName/requestToken for auth/sms/request and verify. Obtain a fresh CSRF token after browser sign-in."
            });
            var previous = o.SwaggerGeneratorOptions.DocInclusionPredicate;
            o.DocInclusionPredicate((name, api) => Includes(name, api, previous));
            o.DocumentFilter<IdentityDocumentFilter>();
            o.OperationFilter<IdentityOperationFilter>();
        });
        return services;
    }

    /// <summary>Use the host module catalog, not a module-local Swagger options instance.
    /// Private Swashbuckle assemblies can be isolated by the module loader.</summary>
    public static bool IsStorefrontDocumentationEnabled(IModuleCatalog catalog, IConfiguration configuration, IHostEnvironment environment)
        => catalog.HasModule("BonakMarket.StorefrontApi") &&
            (environment.IsDevelopment() || configuration.GetValue<bool>("StorefrontApi:EnableSwagger"));

    public static void UseIdentitySwagger(this IApplicationBuilder app, IHostEnvironment environment, IModuleCatalog catalog)
    {
        var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var identityEnabled = environment.IsDevelopment() || config.GetValue<bool>("Identity:EnableSwagger");
        // The native module catalog is shared across module load contexts.
        var storefrontEnabled = IsStorefrontDocumentationEnabled(catalog, config, environment);

        // A module owns its JSON. The shared UI never serves an additional JSON route.
        app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/docs/identity"), branch =>
        {
            if (!identityEnabled)
            {
                branch.Run(ctx => { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return Task.CompletedTask; });
                return;
            }

            branch.Use(async (ctx, next) =>
            {
                if (IsSwaggerFile(ctx.Request.Path) && !(IsDocumentPath(ctx.Request.Path, "/docs/identity", DocumentName)))
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await next();
            });
            branch.UseWhen(ctx => IsDocumentPath(ctx.Request.Path, "/docs/identity", DocumentName), docs =>
                docs.UseSwagger(o => o.RouteTemplate = "docs/identity/{documentName}/swagger.json"));
            branch.UseSwaggerUI(o =>
            {
                o.RoutePrefix = "docs/identity";
                o.SwaggerEndpoint("identity/swagger.json", "Identity API v1");
                o.EnableFilter();
                o.DisplayRequestDuration();
            });
        });

        app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/docs/customer"), branch =>
        {
            if (!identityEnabled && !storefrontEnabled)
            {
                branch.Run(ctx => { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return Task.CompletedTask; });
                return;
            }

            branch.Use(async (ctx, next) =>
            {
                if (IsSwaggerFile(ctx.Request.Path))
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await next();
            });
            branch.UseSwaggerUI(o =>
            {
                o.RoutePrefix = "docs/customer";
                if (identityEnabled)
                    o.SwaggerEndpoint("../identity/identity/swagger.json", "Identity API v1");
                if (storefrontEnabled)
                    o.SwaggerEndpoint("../storefront/storefront/swagger.json", "Storefront API v1");
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

    public static bool IsDocumentPath(PathString path, string prefix, string name) =>
        string.Equals(path.Value?.TrimEnd('/'), prefix + "/" + name + "/swagger.json", StringComparison.OrdinalIgnoreCase);
}
