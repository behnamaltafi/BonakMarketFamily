using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BonakMarket.Identity.Swagger;

/// <summary>Identity owns its document and the optional customer documentation portal.
/// The portal discovers registered documents; it does not reference the commerce module.</summary>
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
                Description = "Customer sign-in, session, profile and account security. Start with GET /api/identity/v1/auth/session. Preserve the antiforgery cookie and send the returned headerName/requestToken for SMS request and verify. Obtain a fresh CSRF token after sign-in for cookie mutations."
            });
            var previous = o.SwaggerGeneratorOptions.DocInclusionPredicate;
            o.DocInclusionPredicate((name, api) => Includes(name, api, previous));
            o.DocumentFilter<IdentityDocumentFilter>();
            o.OperationFilter<IdentityOperationFilter>();
        });
        return services;
    }

    public static void UseIdentitySwagger(this IApplicationBuilder app, IHostEnvironment environment)
    {
        var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var identityEnabled = environment.IsDevelopment() || config.GetValue<bool>("Identity:EnableSwagger");
        // Storefront is optional: inspect document registration rather than coupling module assemblies.
        var documents = app.ApplicationServices.GetRequiredService<IOptions<SwaggerGeneratorOptions>>().Value.SwaggerDocs;
        var storefrontEnabled = documents.ContainsKey("storefront") &&
            (environment.IsDevelopment() || config.GetValue<bool>("StorefrontApi:EnableSwagger"));

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
