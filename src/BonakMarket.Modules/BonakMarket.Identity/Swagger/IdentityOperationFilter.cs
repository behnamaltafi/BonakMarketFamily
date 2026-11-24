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
        var mobileSms = route.StartsWith("auth/mobile/sms/");
        var session = route == "auth/session";
        var refresh = route == "auth/refresh";
        var privateAccount = route.StartsWith("me/") || route == "auth/logout";

        operation.Tags = new List<OpenApiTag> { new() { Name =
            mobileSms ? "Authentication - Mobile SMS" :
            sms ? "Authentication - SMS" :
            route.StartsWith("me/security/") ? "Account Security" :
            route.StartsWith("me/addresses") ? "Addresses" :
            route == "guilds" ? "Guilds" :
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

        if (mobileSms)
        {
            operation.Description = (operation.Description ?? "") + " Token-only JSON over HTTPS. No cookie or CSRF token required.";
            AddResponse(operation, "400", "Invalid phone, challenge, OTP or HTTPS requirement.");
            AddResponse(operation, "404", "SMS login is disabled.");
            AddResponse(operation, "503", "Mobile token issuance unavailable.");
        }
        else if (sms)
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
            if (route.StartsWith("me/security/"))
                AddResponse(operation, "404", "Phone profile or SMS login capability disabled.");
            if (route.StartsWith("me/addresses"))
            {
                AddResponse(operation, "404", "Address not found or not owned by current customer.");
                if (method != "GET") { AddResponse(operation, "409", "Address conflict."); AddResponse(operation, "422", "Address validation failed."); }
            }
            if (route == "me/profile" && method == "PUT")
                AddResponse(operation, "422", "Profile validation failed (ProblemDetails).");
        }
        AddResponse(operation, "400", "Invalid request.");
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
