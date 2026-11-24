using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BonakMarket.StorefrontApi.Security;

internal sealed class StorefrontDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        if (document.Info.Title != "BonakMarket Storefront API") return;
        // Do not inherit Basic authentication from the ERP/OData document.
        document.SecurityRequirements.Clear();
        document.Components.SecuritySchemes.Clear();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = "Access token returned by SMS verification."
        };
        foreach (var path in document.Paths)
        {
            foreach (var operation in path.Value.Operations.Values)
            {
                operation.Security.Clear();
                if (path.Key.StartsWith("/api/storefront/v1/orders", StringComparison.OrdinalIgnoreCase) ||
                    path.Key.StartsWith("/api/storefront/v1/cart", StringComparison.OrdinalIgnoreCase) ||
                    path.Key.StartsWith("/api/sms-login/profile", StringComparison.OrdinalIgnoreCase) ||
                    path.Key.StartsWith("/api/sms-login/enroll", StringComparison.OrdinalIgnoreCase) ||
                    path.Key.Equals("/api/sms-login/logout", StringComparison.OrdinalIgnoreCase))
                {
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
                    });
                    // Browser cookie sessions are also supported; Swagger cannot set the HttpOnly cookie.
                    operation.Description = (operation.Description ?? "") + " Browser cookie authentication is also supported with the antiforgery request header on mutations.";
                }
                else if (path.Key.StartsWith("/api/storefront/v1/", StringComparison.OrdinalIgnoreCase) ||
                    path.Key.Equals("/api/sms-login/session", StringComparison.OrdinalIgnoreCase))
                {
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
                    });
                    operation.Security.Add(new OpenApiSecurityRequirement());
                }
            }
        }
    }
}
