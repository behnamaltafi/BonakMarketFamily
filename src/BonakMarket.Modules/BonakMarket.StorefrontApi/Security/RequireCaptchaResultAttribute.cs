using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BonakMarket.StorefrontApi.Security;

/// <summary>Fail closed when the native CAPTCHA filter cannot validate a request.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireCaptchaResultAttribute : ActionFilterAttribute
{
    public RequireCaptchaResultAttribute() => Order = 100;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ActionArguments.TryGetValue("captchaValid", out var valid) || valid is not true)
            context.Result = new ObjectResult(new ProblemDetails { Status = 422, Title = "captcha_validation_failed" })
            { StatusCode = 422, ContentTypes = { "application/problem+json" } };
    }
}
