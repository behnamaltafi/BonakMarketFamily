using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

// The module filter accepts verified Bearer identity (or guest for public routes)
// and checks native permissions. AllowAnonymous avoids host MVC cookie redirects.
[ApiController, AllowAnonymous, IgnoreAntiforgeryToken]
[TypeFilter(typeof(StorefrontAuthorizationFilter))]
[ApiExplorerSettings(GroupName = "storefront")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class StorefrontController : ControllerBase
{
    protected ObjectResult Error(int status, string code) => Problem(statusCode: status, title: code);

    protected ObjectResult FailureResponse(StorefrontFailure failure)
    {
        if (failure.Extensions == null)
            return Error(failure.Status, failure.Code);

        // These enriched checkout errors were returned as explicit 422 ProblemDetails.
        var problem = new ProblemDetails { Status = failure.Status, Title = failure.Code };
        foreach (var entry in failure.Extensions)
            problem.Extensions[entry.Key] = entry.Value;
        return UnprocessableEntity(problem);
    }

    protected ActionResult<T> Respond<T>(StorefrontResult<T> result, int status = 200)
    {
        if (result.Failure != null)
            return FailureResponse(result.Failure);
        return status == 200 ? Ok(result.Value) : StatusCode(status, result.Value);
    }

    protected IActionResult RespondNoContent(StorefrontResult<bool> result) =>
        result.Failure != null ? FailureResponse(result.Failure) : NoContent();
}
