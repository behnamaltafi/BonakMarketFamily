#nullable enable
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Common;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

internal sealed class CheckoutOptions(SmartDbContext db, StorefrontRequestContext request, IAddressService addresses,
    IShippingService shipping, IPaymentService payments)
{
    private readonly CheckoutAddressChecks _addressChecks = new(db, request, addresses);

    internal async Task<List<CheckoutShippingOptionDto>?> QuoteShipping(ShoppingCart cart, int addressId, CancellationToken ct)
    {
        var address = await _addressChecks.OwnedAddress(addressId, ct);
        if (address == null || !await _addressChecks.ValidAddress(address, true)) return null;
        var quote = await shipping.CreateShippingOptionRequestAsync(cart, address, request.StoreId);
        var result = await shipping.GetShippingOptionsAsync(quote);
        if (result.ShippingOptions.Count == 0) return null;
        return result.ShippingOptions.Select(x => new CheckoutShippingOptionDto(x.ShippingMethodId,
            x.ShippingRateComputationMethodSystemName, x.Name, x.Description, x.Rate)).ToList();
    }

    internal async Task<List<CheckoutPaymentOptionDto>> AvailablePaymentMethods(ShoppingCart cart)
    {
        if (!cart.Requirements.HasFlag(CheckoutRequirements.Payment)) return [];
        var methods = await payments.LoadActivePaymentProvidersAsync(cart, request.StoreId,
            PaymentMethodType.Standard | PaymentMethodType.Redirection);
        return methods.Where(x => x.Value.DisplayInPaymentMethodList &&
            (!cart.ContainsRecurringItem() || x.Value.RecurringPaymentType > RecurringPaymentType.NotSupported))
            .Select(x => new CheckoutPaymentOptionDto(x.Metadata.SystemName,
                x.Metadata.FriendlyName ?? x.Metadata.SystemName, x.Metadata.Description, x.Value.RequiresInteraction,
                x.Value.RequiresConfirmation, x.Value.RequiresPaymentSelection)).ToList();
    }
}
