using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.StorefrontApi.Domain;

namespace BonakMarket.StorefrontApi.Security;

public sealed class StorefrontAuthorizationFilter(
    IWorkContext work, IStoreContext stores, IPermissionService permissions,
    StorefrontRequestContext request) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var action = context.ActionDescriptor as ControllerActionDescriptor;
        if (action == null) { Deny(context, 403, "permission_denied"); return; }
        var isPublic = action.MethodInfo.IsDefined(typeof(PublicCatalogAttribute), true);
        var guestAction = action.MethodInfo.IsDefined(typeof(AllowGuestCartAttribute), true);
        var guestCreate = action.MethodInfo.IsDefined(typeof(CreateGuestCartAttribute), true);
        var guestMerge = action.MethodInfo.IsDefined(typeof(MergeGuestCartAttribute), true);
        var bearer = http.Request.Headers.ContainsKey("Authorization");
        Customer identity = null;
        // Keep the same secure-by-default transport policy as Identity without a module assembly dependency.
        var configuration = http.RequestServices?.GetService<IConfiguration>();
        if (!http.Request.IsHttps && !(bool.TryParse(configuration?["Identity:RequireHttps"], out var requireHttps) && !requireHttps))
        { Deny(context, 400, "https_required"); return; }

        if (bearer)
        {
            identity = http.Items.TryGetValue(typeof(IMobileApiTokenService), out var verified) ? verified as Customer : null;
            if (identity == null) { Deny(context, 401, "invalid_access_token"); return; }
        }

        var guestToken = http.Items.TryGetValue(typeof(GuestCartToken), out var token) ? token as GuestCartToken : null;
        if (identity == null && !isPublic && !guestCreate && !(guestAction && guestToken != null))
        { Deny(context, 401, guestAction ? "guest_cart_token_required" : "authentication_required"); return; }
        if (guestCreate && identity != null)
        { Deny(context, 409, "guest_cart_creation_requires_anonymous"); return; }
        // If routing/middleware metadata was not applied, fail closed instead of bypassing rate limits or locks.
        if (guestCreate && !http.Items.ContainsKey(GuestCartPolicy.CreationAllowed) ||
            guestMerge && !http.Items.ContainsKey(GuestCartPolicy.MergeAllowed))
        { Deny(context, 403, "guest_cart_context_required"); return; }
        if (identity == null && (guestAction || guestCreate) && !GuestCartPolicy.Enabled(configuration))
        { Deny(context, 403, "guest_cart_disabled"); return; }
        var customer = work.CurrentCustomer;
        if (customer == null || work.CurrentImpersonator != null ||
            (identity != null && customer.Id != identity.Id) ||
            (identity == null && customer.IsRegistered()))
        { Deny(context, 403, "identity_mismatch"); return; }

        var storeId = stores.CurrentStore.Id;
        if (storeId <= 0) { Deny(context, 503, "store_unavailable"); return; }
        if (identity == null && (guestAction || guestCreate || guestToken != null) &&
            (!GuestCartPolicy.IsGuest(customer) || (!guestCreate && (guestToken == null || guestToken.CustomerId != customer.Id || guestToken.StoreId != storeId))))
        { Deny(context, 403, "identity_mismatch"); return; }
        if (!await permissions.AuthorizeAsync(Permissions.System.AccessShop, customer))
        { Deny(context, 403, "shop_access_denied"); return; }

        if (action != null)
        {
            var required = action.ControllerTypeInfo.GetCustomAttributes(typeof(StorefrontPermissionAttribute), true)
                .Concat(action.MethodInfo.GetCustomAttributes(typeof(StorefrontPermissionAttribute), true))
                .Cast<StorefrontPermissionAttribute>();
            foreach (var permission in required)
            {
                var name = identity == null && (guestAction || guestCreate) && permission.Permission == StorefrontPermissions.Cart
                    ? StorefrontPermissions.GuestCart : permission.Permission;
                if (!await permissions.AuthorizeAsync(name, customer))
                { Deny(context, 403, "permission_denied"); return; }
            }
        }

        request.Customer = customer;
        request.StoreId = storeId;
        request.IsGuestCart = identity == null && guestToken != null;
    }

    private static void Deny(AuthorizationFilterContext context, int status, string code)
    {
        if (status == 401) context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Title = code })
        { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
