using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

// The module filter authenticates JWT/cookie, validates cookie CSRF and checks native permissions
// before any action executes. AllowAnonymous avoids the host MVC cookie redirect.
[ApiController, AllowAnonymous, IgnoreAntiforgeryToken]
[TypeFilter(typeof(StorefrontAuthorizationFilter))]
[ApiExplorerSettings(GroupName = "storefront")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class StorefrontController : ControllerBase
{
    protected ObjectResult Error(int status, string code) => Problem(statusCode: status, title: code);
}
