#nullable enable
using Microsoft.EntityFrameworkCore;
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

public sealed class CheckoutSessionState(SmartDbContext db, StorefrontRequestContext request, CheckoutAddressChecks addressChecks)
{

    internal async Task<string> RawCartFingerprint(CancellationToken ct)
    {
        var rows = await StorefrontScope.Cart(db, request.Customer, request.StoreId).AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ProductId, x.Quantity, x.Active, x.ParentItemId, x.BundleItemId,
                x.RawAttributes, x.CustomerEnteredPrice, x.UpdatedOnUtc }).ToListAsync(ct);
        var ga = request.Customer.GenericAttributes;
        return CartSnapshotRevision.Create(new { Rows = rows, ga.DiscountCouponCode, ga.RawGiftCardCouponCodes,
            ga.RawCheckoutAttributes, ga.UseRewardPointsDuringCheckout, ga.UseCreditBalanceDuringCheckout,
            request.Customer.GuildId, request.Customer.Email });
    }

    internal bool NativeSelectionsMatch(CheckoutDraft draft)
    {
        var customer = request.Customer;
        var ga = customer.GenericAttributes;
        var option = ga.SelectedShippingOption;
        return customer.BillingAddressId == draft.BillingAddressId && customer.ShippingAddressId == draft.ShippingAddressId &&
            string.Equals(ga.SelectedPaymentMethod, draft.Payment?.SystemName, StringComparison.OrdinalIgnoreCase) &&
            (draft.Shipping == null ? option == null : option != null && option.ShippingMethodId == draft.Shipping.Id &&
                string.Equals(option.ShippingRateComputationMethodSystemName, draft.Shipping.Provider, StringComparison.OrdinalIgnoreCase) &&
                option.Rate == draft.Shipping.Rate);
    }

    internal static bool AddressesComplete(CheckoutDraft draft, ShoppingCart cart) =>
        (!cart.Requirements.HasFlag(CheckoutRequirements.BillingAddress) || draft.BillingAddressId.HasValue) &&
        (!cart.IsShippingRequired || draft.ShippingAddressId.HasValue);

    internal static bool ShippingComplete(CheckoutDraft draft, ShoppingCart cart) =>
        AddressesComplete(draft, cart) && (!cart.IsShippingRequired || draft.Shipping != null);

    internal async Task<CheckoutSessionDto> ToDto(CheckoutDraft draft, CartStateDto state, ShoppingCart native, CancellationToken ct, Func<string> checkoutUrl)
    {
        var blockers = state.Checkout.Blockers.ToList();
        var expired = draft.ExpiresAt <= DateTime.UtcNow;
        if (expired) blockers.Add("checkout_session_expired");
        if (draft.PreparedCartFingerprint == null
            ? !string.Equals(draft.CartRevision, state.Revision, StringComparison.Ordinal)
            : draft.PreparedCartFingerprint != await RawCartFingerprint(ct) || !NativeSelectionsMatch(draft))
            blockers.Add("checkout_cart_changed");
        if (!await addressChecks.AddressesCurrent(draft, ct)) blockers.Add("checkout_address_changed");
        var status = expired ? "expired" : blockers.Count > 0 ? "stale" : "inProgress";
        var completed = new List<string>();
        var needsBilling = native.Requirements.HasFlag(CheckoutRequirements.BillingAddress);
        var needsPayment = native.Requirements.HasFlag(CheckoutRequirements.Payment);
        var addressesComplete = AddressesComplete(draft, native);
        if (addressesComplete) completed.Add("addresses");
        var shippingComplete = ShippingComplete(draft, native);
        if (shippingComplete && native.IsShippingRequired) completed.Add("shippingMethod");
        var paymentComplete = !needsPayment || draft.Payment != null;
        if (shippingComplete && needsPayment && paymentComplete) completed.Add("paymentMethod");
        var ready = blockers.Count == 0 && shippingComplete && paymentComplete;
        var step = status != "inProgress" ? "restart" : !addressesComplete ? "addresses" :
            !shippingComplete ? "shippingMethod" : !paymentComplete ? "paymentMethod" : "review";
        return new(draft.Id, draft.Revision, draft.CartRevision, ready ? (draft.PreparedCartFingerprint != null ? "reviewPrepared" : "readyForReview") : status, step,
            completed, draft.ExpiresAt, needsBilling, native.IsShippingRequired, needsPayment,
            draft.BillingAddressId, draft.ShippingAddressId, request.Customer.GenericAttributes.DefaultBillingAddressId,
            request.Customer.GenericAttributes.DefaultShippingAddressId, draft.Shipping, draft.Payment,
            state.Summary with { PayableTotal = null }, ready, false, blockers.Distinct().ToArray(),
            state.Warnings, true, checkoutUrl());
    }
}
