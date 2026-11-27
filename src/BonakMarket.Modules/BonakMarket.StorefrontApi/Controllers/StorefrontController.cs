using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Security;

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
}
