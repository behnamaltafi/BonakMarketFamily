using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

/// <summary>Native checkout preflight and provider quotes. The browser owns payment and order placement.</summary>
[Route("api/storefront/v1/checkout")]
[StorefrontPermission(StorefrontPermissions.Checkout)]
[StorefrontPermission(StorefrontPermissions.Cart)]
[StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
public sealed class CheckoutController(SmartDbContext db, StorefrontRequestContext request, IShoppingCartService carts,
    IShoppingCartValidator validator, IShippingService shipping, IPaymentService payments, IAddressService addresses) : StorefrontController
{
    private Task<ShoppingCart> CartAsync() => carts.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId);

    [HttpGet("readiness")]
    public async Task<ActionResult<CheckoutReadinessDto>> Readiness(CancellationToken ct)
    {
        var cart = await CartAsync();
        var blockers = new List<string>();
        if (await CustomerGuildPolicy.RequiresGuildAsync(db, request.Customer, ct)) blockers.Add("profile_guild_required");
        if (!cart.HasItems) blockers.Add("cart_empty");
        var billing = request.Customer.GenericAttributes.DefaultBillingAddressId;
        var delivery = request.Customer.GenericAttributes.DefaultShippingAddressId;
        return Ok(new CheckoutReadinessDto(blockers.Count == 0, blockers.ToArray(), cart.IsShippingRequired,
            billing, delivery, Url.RouteUrl("Checkout") ?? Url.Content("~/checkout"), true));
    }

    [HttpPost("start")]
    public async Task<ActionResult<CheckoutReadinessDto>> Start(CancellationToken ct)
    {
        if (await CustomerGuildPolicy.RequiresGuildAsync(db, request.Customer, ct))
            return Error(422, "profile_guild_required");
        var cart = await CartAsync();
        if (!cart.HasItems) return Error(422, "cart_empty");
        var warnings = new List<string>();
        if (!await validator.ValidateCartAsync(cart, warnings, true, true))
        {
            var problem = new ProblemDetails { Status = 422, Title = "cart_validation_failed" };
            problem.Extensions["warnings"] = warnings;
            return UnprocessableEntity(problem);
        }
        return await Readiness(ct);
    }

    [HttpGet("shipping-options")]
    [StorefrontPermission(Permissions.Catalog.DisplayPrice)]
    public async Task<ActionResult<List<CheckoutShippingOptionDto>>> ShippingOptions(int addressId, CancellationToken ct)
    {
        if (await CustomerGuildPolicy.RequiresGuildAsync(db, request.Customer, ct)) return Error(422, "profile_guild_required");
        var cart = await CartAsync();
        if (!cart.HasItems) return Error(422, "cart_empty");
        if (!cart.IsShippingRequired) return Ok(new List<CheckoutShippingOptionDto>());
        var address = await db.Customers.Where(x => x.Id == request.Customer.Id).SelectMany(x => x.Addresses)
            .Include(x => x.Country).Include(x => x.StateProvince).FirstOrDefaultAsync(x => x.Id == addressId, ct);
        if (address == null) return Error(404, "address_not_found");
        if (address.Country?.TwoLetterIsoCode != "IR" || !address.Country.Published || !address.Country.AllowsShipping ||
            !await addresses.IsAddressValidAsync(address)) return Error(422, "invalid_shipping_address");
        var quote = await shipping.CreateShippingOptionRequestAsync(cart, address, request.StoreId);
        var response = await shipping.GetShippingOptionsAsync(quote);
        if (response.ShippingOptions.Count == 0 && !response.Success) return Error(422, "shipping_unavailable");
        return Ok(response.ShippingOptions.Select(x => new CheckoutShippingOptionDto(x.ShippingMethodId,
            x.ShippingRateComputationMethodSystemName, x.Name, x.Description, x.Rate)).ToList());
    }

    [HttpGet("payment-methods")]
    public async Task<ActionResult<List<CheckoutPaymentMethodDto>>> PaymentMethods(CancellationToken ct)
    {
        if (await CustomerGuildPolicy.RequiresGuildAsync(db, request.Customer, ct)) return Error(422, "profile_guild_required");
        var cart = await CartAsync();
        if (!cart.HasItems) return Error(422, "cart_empty");
        var methods = await payments.LoadActivePaymentProvidersAsync(cart, request.StoreId,
            PaymentMethodType.Standard | PaymentMethodType.Redirection);
        return Ok(methods.Where(x => x.Value.DisplayInPaymentMethodList &&
            (!cart.ContainsRecurringItem() || x.Value.RecurringPaymentType > RecurringPaymentType.NotSupported))
            .Select(x => new CheckoutPaymentMethodDto(x.Metadata.SystemName, x.Value.RequiresInteraction,
                x.Value.RequiresConfirmation)).ToList());
    }
}
