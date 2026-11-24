using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using BonakMarket.Core;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;

namespace BonakMarket.StorefrontApi.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class StorefrontPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PublicCatalogAttribute : Attribute { }

public sealed class StorefrontRequestContext
{
    public Customer Customer { get; internal set; }
    public int StoreId { get; internal set; }
}

public sealed class StorefrontAuthorizationFilter(
    IWorkContext work, IStoreContext stores, IPermissionService permissions,
    UserManager<Customer> users, IAntiforgery antiforgery, StorefrontRequestContext request) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var action = context.ActionDescriptor as ControllerActionDescriptor;
        if (action == null) { Deny(context, 403, "permission_denied"); return; }
        var isPublic = action.MethodInfo.IsDefined(typeof(PublicCatalogAttribute), true);
        var bearer = http.Request.Headers.ContainsKey("Authorization");
        Customer identity = null;
        if (!http.Request.IsHttps) { Deny(context, 400, "https_required"); return; }

        if (bearer)
        {
            identity = http.Items.TryGetValue(StorefrontBearerMiddleware.CustomerKey, out var verified) ? verified as Customer : null;
            if (identity == null) { Deny(context, 401, "invalid_access_token"); return; }
        }
        else
        {
            var auth = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (auth.Succeeded && auth.Principal != null)
                identity = await users.GetUserAsync(auth.Principal);
            if (identity != null && (!identity.Active || identity.Deleted || !identity.IsRegistered() || identity.IsSystemAccount))
                identity = null;
        }

        if (identity == null && !isPublic) { Deny(context, 401, "authentication_required"); return; }
        var customer = work.CurrentCustomer;
        if (customer == null || work.CurrentImpersonator != null ||
            (identity != null && customer.Id != identity.Id) ||
            (identity == null && customer.IsRegistered()))
        { Deny(context, 403, "identity_mismatch"); return; }

        if (!bearer && !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
        {
            try { await antiforgery.ValidateRequestAsync(http); }
            catch (AntiforgeryValidationException) { Deny(context, 400, "invalid_antiforgery_token"); return; }
        }

        var storeId = stores.CurrentStore.Id;
        if (storeId <= 0) { Deny(context, 503, "store_unavailable"); return; }
        if (!await permissions.AuthorizeAsync(Permissions.System.AccessShop, customer))
        { Deny(context, 403, "shop_access_denied"); return; }

        if (action != null)
        {
            var required = action.ControllerTypeInfo.GetCustomAttributes(typeof(StorefrontPermissionAttribute), true)
                .Concat(action.MethodInfo.GetCustomAttributes(typeof(StorefrontPermissionAttribute), true))
                .Cast<StorefrontPermissionAttribute>();
            foreach (var permission in required)
                if (!await permissions.AuthorizeAsync(permission.Permission, customer))
                { Deny(context, 403, "permission_denied"); return; }
        }

        request.Customer = customer;
        request.StoreId = storeId;
    }

    private static void Deny(AuthorizationFilterContext context, int status, string code)
    {
        if (status == 401) context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Title = code })
        { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
