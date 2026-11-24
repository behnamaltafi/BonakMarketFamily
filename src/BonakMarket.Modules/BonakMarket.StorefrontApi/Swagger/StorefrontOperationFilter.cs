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
        var tag = route.EndsWith("/share") ? "Sharing" :
            route.Contains("/reviews") ? "Reviews" :
            route.Contains("/return-options") || route.Contains("/returns") ? "Returns" :
            route.Contains("/shipments") ? "Shipments" : null;
        operation.Tags = new List<OpenApiTag> { new() { Name = tag ?? (section switch
        {
            "products" => "Products",
            "categories" => "Categories",
            "brands" => "Brands",
            "cart" => "Cart",
            "orders" => "Orders",
            "wishlist" => "Wishlist",
            "reward-points" => "Reward Points",
            "stock-subscriptions" => "Stock Subscriptions",
            "checkout" => "Checkout",
            "returns" => "Returns",
            "shipments" => "Shipments",
            "recurring-payments" => "Recurring Payments",
            "downloads" => "Downloads",
            _ => "Commerce"
        }) } };
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
        if (section is "cart" or "wishlist")
        {
            if (method is "PUT" or "POST") AddResponse(operation, "409", "Product or cart operation not supported in its current state.");
            if (method == "DELETE") AddResponse(operation, "404", "Item or applied gift card not found.");
            if (route == "cart/total") AddResponse(operation, "422", "Cart calculation unavailable.");
        }
        if (route == "cart/items" && method == "POST")
            AddResponse(operation, "409", "Product configuration is not supported by this API.");

        if (section is "wishlist" or "checkout" or "returns" or "shipments" or "recurring-payments" or "downloads" or "stock-subscriptions" || tag != null)
        {
            AddResponse(operation, "404", "Resource not found in the current customer/store scope.");
            AddResponse(operation, "422", "Native business validation failed.");
        }
        if (section == "wishlist" && method == "POST") AddResponse(operation, "409", "Unsupported product configuration.");

        if (tag == "Sharing") AddResponse(operation, "429", "Sharing rate limit exceeded.");
        foreach (var entry in operation.Responses)
        {
            if (!int.TryParse(entry.Key, out var status) || status < 400 || status >= 600) continue;
            entry.Value.Content ??= new Dictionary<string, OpenApiMediaType>();
            entry.Value.Content.Clear();
            entry.Value.Content["application/problem+json"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = "object",
                    Required = new HashSet<string> { "status", "code", "title", "traceId" },
                    AdditionalPropertiesAllowed = true,
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["status"] = new() { Type = "integer", Format = "int32" },
                        ["code"] = new() { Type = "string", Description = "Stable machine-readable error code." },
                        ["title"] = new() { Type = "string" },
                        ["detail"] = new() { Type = "string", Nullable = true },
                        ["traceId"] = new() { Type = "string" },
                        ["errors"] = new() { Type = "object", AdditionalProperties = new OpenApiSchema
                            { Type = "array", Items = new OpenApiSchema { Type = "string" } } },
                        ["warnings"] = new() { Type = "array", Items = new OpenApiSchema { Type = "string" } }
                    }
                }
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
