using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BonakMarket.Identity.Swagger;

/// <summary>Overrides inherited integration security for this document only.</summary>
internal sealed class IdentityDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        if (document.Info.Title != IdentitySwaggerConfiguration.DocumentTitle) return;

        document.SecurityRequirements = new List<OpenApiSecurityRequirement>();
        document.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            Description = "Customer access token from SMS verification or refresh. Enter the token without the Bearer prefix."
        };
    }
}
