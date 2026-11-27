using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BonakMarket.StorefrontApi.Swagger;

/// <summary>Overrides inherited integration security for this document only.</summary>
internal sealed class StorefrontDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        if (document.Info.Title != StorefrontSwaggerConfiguration.DocumentTitle) return;

        document.SecurityRequirements = new List<OpenApiSecurityRequirement>();
        document.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = "Customer access token from SMS verification or refresh. Enter the token without the Bearer prefix."
        };
        document.Components.SecuritySchemes["GuestCartToken"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Guest-Cart-Token",
            Description = "Opaque guest cart token from POST /cart/guest. Never use as a customer Bearer token."
        };
    }
}
