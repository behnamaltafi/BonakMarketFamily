using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Swagger;

/// <summary>Uses the same public catalog metadata as the runtime authorization filter.</summary>
internal sealed class StorefrontOperationFilter(IOptions<AntiforgeryOptions> antiforgery) : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath;
        if (context.ApiDescription.GroupName != StorefrontSwaggerConfiguration.DocumentName ||
            !StorefrontSwaggerConfiguration.IsStorefrontPath(path)) return;

        var route = path.Substring("api/storefront/v1/".Length).TrimEnd('/').ToLowerInvariant();
        var method = context.ApiDescription.HttpMethod;
        var section = route.Split('/')[0];
        operation.Tags = new List<OpenApiTag> { new() { Name = section switch
        {
            "products" => "Products",
            "categories" => "Categories",
            "brands" => "Brands",
            "cart" => "Cart",
            "orders" => "Orders",
            _ => "Commerce"
        } } };
        // Use the route template and verb (not overloaded CLR method names).
        operation.OperationId = "Storefront_" + method + "_" +
            route.Replace('/', '_').Replace("{", "").Replace("}", "").Replace(":", "_");
        operation.Security = new List<OpenApiSecurityRequirement>
        {
            Requirement("Bearer"),
            Requirement("CustomerCookie")
        };
        if (context.MethodInfo.IsDefined(typeof(PublicCatalogAttribute), true))
            operation.Security.Add(new OpenApiSecurityRequirement());

        if (method != "GET" && method != "HEAD") AddCsrf(operation, false);
        AddResponse(operation, "400", "Invalid request, HTTPS requirement or invalid cookie antiforgery token (ProblemDetails).");
        AddResponse(operation, "401", "Missing or invalid required customer identity / token (ProblemDetails).");
        AddResponse(operation, "403", "Native permission or account/store context denied (ProblemDetails).");
        AddResponse(operation, "503", "Store unavailable (ProblemDetails).");
        if (section == "products" && route != "products" ||
            section == "orders" && route != "orders" ||
            section == "cart" && route.StartsWith("cart/items/"))
            AddResponse(operation, "404", "Requested resource not found.");
        if (section == "cart" && (method == "POST" || method == "PUT"))
            AddResponse(operation, "422", "Cart or coupon validation failed.");
        if (route == "cart/items" && method == "POST")
            AddResponse(operation, "409", "Product configuration is not supported by this API.");

        foreach (var status in new[] { "400", "401", "403", "404", "409", "422", "503" })
        {
            if (!operation.Responses.TryGetValue(status, out var response)) continue;
            response.Content ??= new Dictionary<string, OpenApiMediaType>();
            if (!response.Content.ContainsKey("application/problem+json"))
                response.Content["application/problem+json"] = new OpenApiMediaType
                {
                    Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository)
                };
        }
    }

    private static OpenApiSecurityRequirement Requirement(string scheme) => new()
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = scheme } }] = Array.Empty<string>()
    };

    private static void AddResponse(OpenApiOperation operation, string code, string description)
    {
        if (operation.Responses.TryGetValue(code, out var existing))
            existing.Description = description;
        else
            operation.Responses.Add(code, new OpenApiResponse { Description = description });
    }

    private void AddCsrf(OpenApiOperation operation, bool required)
    {
        var header = antiforgery.Value.HeaderName;
        if (string.IsNullOrWhiteSpace(header)) return;
        operation.Parameters ??= new List<OpenApiParameter>();
        if (operation.Parameters.Any(p => p.In == ParameterLocation.Header &&
            p.Name.Equals(header, StringComparison.OrdinalIgnoreCase))) return;
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = header, In = ParameterLocation.Header, Required = required,
            Schema = new OpenApiSchema { Type = "string" },
            Description = required
                ? "Request token from GET /api/identity/v1/auth/session. Keep the accompanying antiforgery cookie."
                : "Required for cookie-authenticated mutations; omit for Bearer requests. Fetch a fresh token from GET /api/identity/v1/auth/session after signing in."
        });
    }
}
