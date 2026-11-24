using BonakMarket.StorefrontApi.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Common.DTO;
using BonakMarket.Core;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;

namespace BonakMarket.StorefrontApi.Controllers;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class PublicCatalogAttribute : Attribute { }

// All derived actions are guarded here. AllowAnonymous bypasses the host cookie
// challenge only; this filter explicitly authenticates each request and fails closed.
[ApiController, AllowAnonymous, IgnoreAntiforgeryToken]
[ApiExplorerSettings(GroupName = "storefront")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class StorefrontController : Controller
{
    protected Customer Customer { get; private set; }
    protected int StoreId { get; private set; }
    protected IPermissionService PermissionsService => HttpContext.RequestServices.GetRequiredService<IPermissionService>();
    protected IWorkContext Work => HttpContext.RequestServices.GetRequiredService<IWorkContext>();
    protected ObjectResult Error(int status, string message) =>
        StatusCode(status, new ApiResult(message, false, null, status));
    protected OkObjectResult Success<T>(T model) => Ok(new ApiResult<T>(model));

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var sp = HttpContext.RequestServices;
        var publicCatalog = context.ActionDescriptor.EndpointMetadata.OfType<PublicCatalogAttribute>().Any();
        var bearer = Request.Headers.ContainsKey("Authorization");
        Customer identity = null;
        if (!Request.IsHttps)
        {
            context.Result = Error(400, "https_required");
            return;
        }
        if (bearer)
        {
            identity = HttpContext.Items.TryGetValue(StorefrontBearerMiddleware.CustomerKey, out var verified)
                ? verified as Customer : null;
            if (identity == null)
            {
                context.Result = Error(401, "invalid_access_token");
                return;
            }
            if (Work.CurrentImpersonator != null || Work.CurrentCustomer.Id != identity.Id)
            {
                context.Result = Error(403, "identity_mismatch");
                return;
            }
            // Use the native WorkContext populated from the validated ticket.
            identity = Work.CurrentCustomer;
        }
        else
        {
            var auth = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (auth.Succeeded && auth.Principal != null)
                identity = await sp.GetRequiredService<UserManager<Customer>>().GetUserAsync(auth.Principal);
            if (identity != null && (!identity.Active || identity.Deleted || !identity.IsRegistered() || identity.IsSystemAccount))
                identity = null;
            if (identity != null && (Work.CurrentImpersonator != null || Work.CurrentCustomer.Id != identity.Id))
            {
                context.Result = Error(403, "identity_mismatch");
                return;
            }
            // No browser mutation (including anonymous unsafe requests) without CSRF.
            if (!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method) && !publicCatalog)
            {
                try { await sp.GetRequiredService<IAntiforgery>().ValidateRequestAsync(HttpContext); }
                catch (AntiforgeryValidationException)
                {
                    context.Result = Error(400, "invalid_antiforgery_token");
                    return;
                }
            }
        }
        if (identity == null && !publicCatalog)
        {
            context.Result = Error(401, "authentication_required");
            return;
        }
        Customer = identity ?? Work.CurrentCustomer;
        if (Customer == null || (identity == null && Customer.IsRegistered()) ||
            !await PermissionsService.AuthorizeAsync(Permissions.System.AccessShop, Customer))
        {
            context.Result = Error(403, "shop_access_denied");
            return;
        }
        StoreId = sp.GetRequiredService<IStoreContext>().CurrentStore.Id;
        if (StoreId <= 0)
        {
            context.Result = Error(503, "store_unavailable");
            return;
        }
        if (!context.ModelState.IsValid)
        {
            context.Result = Error(400, "invalid_request");
            return;
        }
        await next();
    }
}
