using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BonakMarket.Identity.Swagger;

/// <summary>Documents the actual customer authentication boundary, not MVC AllowAnonymous metadata.</summary>
internal sealed class IdentityOperationFilter(IOptions<AntiforgeryOptions> antiforgery) : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath;
        if (context.ApiDescription.GroupName != IdentitySwaggerConfiguration.DocumentName ||
            !IdentitySwaggerConfiguration.IsIdentityPath(path)) return;

        var route = path.Substring("api/identity/v1/".Length).TrimEnd('/').ToLowerInvariant();
        var method = context.ApiDescription.HttpMethod;
        var sms = route.StartsWith("auth/sms/");
        var session = route == "auth/session";
        var refresh = route == "auth/refresh";
        var privateAccount = route.StartsWith("me/") || route == "auth/logout";

        operation.Tags = new List<OpenApiTag> { new() { Name =
            sms ? "Authentication - SMS" :
            route.StartsWith("me/security/") ? "Account Security" :
            route.StartsWith("me/") ? "Profile" : "Authentication - Session" } };
        operation.OperationId = "Identity_" + method + "_" + route.Replace('/', '_');
        operation.Security = new List<OpenApiSecurityRequirement>();
        if (privateAccount || session)
        {
            operation.Security.Add(Requirement("Bearer"));
            operation.Security.Add(Requirement("CustomerCookie"));
            if (session) operation.Security.Add(new OpenApiSecurityRequirement());
            AddResponse(operation, "401", "Missing or invalid customer identity / token.");
        }

        if (sms)
        {
            AddCsrf(operation, true);
            AddResponse(operation, "400", "Invalid request, OTP or antiforgery token.");
            AddResponse(operation, "404", "SMS login is disabled.");
            if (route.EndsWith("/verify"))
            {
                AddResponse(operation, "401", "Customer token issuance rejected.");
                AddResponse(operation, "503", "Mobile token issuance unavailable.");
            }
        }
        else if (refresh)
        {
            operation.Description = (operation.Description ?? "") +
                " Supply the refresh credential in the request body. No access token or cookie CSRF token is required. Replace both tokens after a successful rotation.";
            AddResponse(operation, "401", "Invalid, expired, revoked or reused refresh credential, or token issuance disabled.");
        }
        else if (privateAccount)
        {
            if (method != "GET" && method != "HEAD") AddCsrf(operation, false);
            AddResponse(operation, "400", "Invalid request, HTTPS requirement or invalid cookie antiforgery token.");
            if (route.StartsWith("me/security/") || (route == "me/profile" && method == "PUT"))
                AddResponse(operation, "404", "Phone profile or SMS login capability disabled.");
            if (route == "me/profile" && method == "PUT")
                AddResponse(operation, "422", "Profile validation failed (ProblemDetails).");
            // Identity preserves legacy mixed error payloads; do not declare a single false schema for them.
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
