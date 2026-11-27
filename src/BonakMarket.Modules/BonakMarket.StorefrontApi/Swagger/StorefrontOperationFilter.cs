using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Swagger;

/// <summary>Uses the same public catalog metadata as the runtime authorization filter.</summary>
internal sealed class StorefrontOperationFilter : IOperationFilter
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
            "bootstrap" => "Storefront",
            "pages" => "Pages",
            "me" => "Customer Services",
            _ => "Commerce"
        }) } };
        switch (route)
        {
            case "products":
                operation.Summary = "List and search products";
                operation.Description = "Optional search text (max 200 characters), 1-based page (1–10000), pageSize (1–100). Filters: categoryId, brandId, minPrice, maxPrice, inStock and hasDiscount. For nonblank searches, omitted inStock follows admin availability settings; true selects sellable products, false selects unavailable products. Price filters and sorting use catalog base-currency prices before customer-specific pricing/tax. Price/discount filtering and price sorting require price-display permission. List prices are indicative unit prices at pricingQuantity (the minimum supported order quantity). Use quote for the selected configuration.";
                break;
            case "products/suggestions":
                operation.Summary = "Instant search suggestions";
                operation.Description = "Optional search text (max 200 characters), optional limit (1–16, capped by admin settings). Disabled, empty, or too-short searches return 200 with empty items. No price or total count; hasMore indicates additional matches. Images may be null or relative to the API origin.";
                break;
            case "products/facets":
                operation.Summary = "Available product filters";
                operation.Description = "Returns total matching products and category/brand counts for the same filters as the product list. Counts are conjunctive and use direct category mappings; lists may be truncated. Send search, categoryId, brandId, minPrice, maxPrice, inStock and hasDiscount as needed. No paging, sorting or price projection. Price/discount filters require price-display permission. Includes availability counts and nullable priceRange (base currency, only when permitted). Counts and bounds respect all supplied filters; clear a filter to see its alternatives.";
                break;
            case "products/{id}":
                operation.Summary = "Product details and purchase options";
                operation.Description = "Includes variants, specifications, images, quantity rules and purchase capabilities. The list price is indicative; use quote for quantity/variant-specific price and validation.";
                break;
            case "products/{id}/quote":
                operation.Summary = "Calculate and validate a product selection";
                operation.Description = "Read-only quote for quantity and selected variant values. HTTP 200 can contain isValid=false and warnings; it never changes the cart or reserves stock. The cart remains authoritative.";
                break;
            case "categories":
                operation.Summary = "List visible categories";
                operation.Description = "Use parentId=0 for root categories, a positive parentId for direct children, or omit parentId for a flat paginated list. Includes localized name, image, hasChildren and showOnHomePage; use showOnHomePage to filter.";
                break;
            case "categories/{id}":
                operation.Summary = "Get a visible category";
                operation.Description = "Same item contract as the category list. Returns 404 for missing or inaccessible categories.";
                break;
            case "brands":
                operation.Summary = "List visible brands";
                operation.Description = "Localized name search, pagination and administrator display order. Includes nullable imageUrl.";
                break;
            case "brands/{id}":
                operation.Summary = "Get a visible brand";
                operation.Description = "Same item contract as the brand list. Returns 404 for missing or inaccessible brands.";
                break;
        }
        // Use the route template and verb (not overloaded CLR method names).
        operation.OperationId = "Storefront_" + method + "_" +
            route.Replace('/', '_').Replace("{", "").Replace("}", "").Replace(":", "_");
        operation.Security = new List<OpenApiSecurityRequirement>
        {
            Requirement("Bearer")
        };
        if (context.MethodInfo.IsDefined(typeof(PublicCatalogAttribute), true))
            operation.Security.Add(new OpenApiSecurityRequirement());

        AddResponse(operation, "400", "Invalid request or HTTPS requirement (ProblemDetails).");
        AddResponse(operation, "401", "Missing or invalid required customer identity / token (ProblemDetails).");
        AddResponse(operation, "403", "Native permission or account/store context denied (ProblemDetails).");
        AddResponse(operation, "503", "Store unavailable (ProblemDetails).");
        if ((section is "products" or "categories" or "brands") && route.StartsWith(section + "/{") ||
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

        if (route.EndsWith("/confirm") && method == "POST" && section == "checkout")
        {
            var key = operation.Parameters?.FirstOrDefault(x => x.Name == "Idempotency-Key");
            if (key != null) key.Required = true;
        }
        if (route.StartsWith("checkout/sessions") && route != "checkout/sessions")
            AddResponse(operation, "409", "Checkout expired, cart/address/rate changed, or stale draft revision.");
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

}
