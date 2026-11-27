using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using BonakMarket.Identity.Models;

namespace BonakMarket.Identity.Filters;

/// <summary>Normalizes MVC client errors for the two canonical customer APIs only.
/// Identity owns the shared customer HTTP boundary; legacy, admin and OData routes are not affected.</summary>
public sealed class CustomerApiProblemFilter : IAlwaysRunResultFilter, IOrderedFilter
{
    public int Order => int.MaxValue;

    public static bool IsCanonicalPath(PathString path) =>
        path.StartsWithSegments("/api/identity/v1") || path.StartsWithSegments("/api/storefront/v1");

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (IsCanonicalPath(context.HttpContext.Request.Path))
            context.Result = Normalize(context.Result, context.HttpContext);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }

    public static IActionResult Normalize(IActionResult result, HttpContext http)
    {
        var value = (result as ObjectResult)?.Value;
        var status = (result as IStatusCodeActionResult)?.StatusCode ?? (value as ProblemDetails)?.Status;
        if (status is not (>= 400 and < 600)) return result;

        var problem = value as ProblemDetails ?? new ProblemDetails();
        problem.Status = status;
        var explicitCode = (value as IdentityCodeResponse)?.Code ?? (value as OtpErrorResponse)?.Code;
        if (value is OtpErrorResponse { RetryAfter: int retryAfter })
            problem.Extensions["retryAfter"] = retryAfter;
        // Native storefront already uses stable snake_case titles. Preserve them.
        if (string.IsNullOrWhiteSpace(explicitCode) && problem.Title != null &&
            Regex.IsMatch(problem.Title, "^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$"))
            explicitCode = problem.Title;

        var code = explicitCode ?? (status switch
        {
            400 when problem is ValidationProblemDetails => "validation_failed",
            400 => "invalid_request",
            401 when http.Request.Path.StartsWithSegments("/api/identity/v1/auth/refresh") => "invalid_refresh_token",
            401 when http.Request.Headers.ContainsKey("Authorization") => "invalid_access_token",
            401 => "authentication_required",
            403 => "permission_denied",
            404 when http.Request.Path.StartsWithSegments("/api/identity/v1/me/addresses") => "address_not_found",
            404 => "not_found",
            409 => "conflict",
            412 => "stale_revision",
            422 => "validation_failed",
            429 => "rate_limited",
            503 => "service_unavailable",
            >= 500 => "internal_error",
            _ => "request_failed"
        });
        problem.Title ??= code;
        if (!problem.Extensions.ContainsKey("code")) problem.Extensions["code"] = code;
        if (!problem.Extensions.ContainsKey("traceId"))
            problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;

        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
