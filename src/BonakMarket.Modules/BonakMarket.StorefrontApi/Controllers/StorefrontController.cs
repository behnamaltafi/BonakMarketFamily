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

    protected async Task<ObjectResult> CartFailureAsync(CartChange result, CartFacade cart)
    {
        var problem = new ProblemDetails { Status = result.Status, Title = result.Code };
        if (result.Warnings?.Count > 0 && await cart.CanDisplayPriceAsync()) problem.Extensions["warnings"] = result.Warnings;
        return new ObjectResult(problem) { StatusCode = result.Status, ContentTypes = { "application/problem+json" } };
    }
}
