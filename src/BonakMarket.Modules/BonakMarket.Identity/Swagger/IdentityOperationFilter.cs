using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BonakMarket.Identity.Swagger;

/// <summary>Documents the actual customer authentication boundary, not MVC AllowAnonymous metadata.</summary>
internal sealed class IdentityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath;
        if (context.ApiDescription.GroupName != IdentitySwaggerConfiguration.DocumentName ||
            !IdentitySwaggerConfiguration.IsIdentityPath(path)) return;

        var route = path.Substring("api/identity/v1/".Length).TrimEnd('/').ToLowerInvariant();
        var method = context.ApiDescription.HttpMethod;
        var sms = route.StartsWith("auth/sms/");
        var logout = route == "auth/logout";
        var refresh = route == "auth/refresh";
        var privateAccount = route.StartsWith("me/");

        operation.Tags = new List<OpenApiTag> { new() { Name =
            sms ? "Authentication" :
            route.StartsWith("me/security/") ? "Account Security" :
            route.StartsWith("me/addresses") ? "Addresses" :
            route == "guilds" ? "Guilds" :
            route.StartsWith("locations/") ? "Locations" :
            route.StartsWith("me/") ? "Profile" : "Authentication" } };
        operation.OperationId = "Identity_" + method + "_" + route.Replace('/', '_');
        operation.Security = new List<OpenApiSecurityRequirement>();
        if (privateAccount || logout)
        {
            operation.Security.Add(Requirement("Bearer"));
            if (logout) operation.Security.Add(new OpenApiSecurityRequirement());
            AddResponse(operation, "401", "Missing, invalid, expired or revoked credential.");
        }

        if (sms)
        {
            operation.Description = (operation.Description ?? "") + " Token-only JSON for web and mobile. HTTPS required by default. No cookie or CSRF token required.";
            AddResponse(operation, "400", "Invalid phone, challenge, OTP or HTTPS requirement.");
            AddResponse(operation, "404", "SMS login is disabled.");
            AddResponse(operation, "429", "OTP quota or verification limit; honor Retry-After and retryAfter when supplied.");
            AddResponse(operation, "503", "Customer token issuance unavailable.");
        }
        else if (logout)
        {
            operation.Description = "Send a valid Bearer access token with no body, OR omit Authorization and send the current refreshToken in the JSON body. Never send both. Revokes only the current API session; MVC cookies are unaffected.";
        }
        else if (refresh)
        {
            operation.Description = (operation.Description ?? "") +
                " Supply the refresh credential in the request body. No access token or cookie CSRF token is required. Replace both tokens after a successful rotation.";
            AddResponse(operation, "401", "Invalid, expired, revoked or reused refresh credential, or token issuance disabled.");
        }
        else if (privateAccount)
        {
            AddResponse(operation, "400", "Invalid request or HTTPS requirement.");
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
                        ["retryAfter"] = new() { Type = "integer", Format = "int32", Description = "Optional seconds until retry." },
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
