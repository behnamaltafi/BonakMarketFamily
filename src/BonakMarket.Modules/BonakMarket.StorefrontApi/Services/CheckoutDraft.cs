#nullable enable
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Services;

/// <summary>Only the provisional API selections are persisted; native browser/payment state is not overwritten.</summary>
internal sealed record CheckoutDraft(Guid Id, int Revision, string CartRevision, DateTime ExpiresAt,
    int? BillingAddressId = null, int? ShippingAddressId = null, string? BillingFingerprint = null,
    string? ShippingFingerprint = null, CheckoutShippingOptionDto? Shipping = null,
    CheckoutPaymentOptionDto? Payment = null)
{
    public static CheckoutDraft Create(string cartRevision, DateTime now) =>
        new(Guid.NewGuid(), 1, cartRevision, now.AddMinutes(30));

    public bool IsCurrent(string cartRevision, DateTime now) =>
        ExpiresAt > now && string.Equals(CartRevision, cartRevision, StringComparison.Ordinal);

    public CheckoutDraft WithAddresses(int? billing, int? shipping, string? billingFingerprint, string? shippingFingerprint) =>
        this with { BillingAddressId = billing, ShippingAddressId = shipping, BillingFingerprint = billingFingerprint,
            ShippingFingerprint = shippingFingerprint, Shipping = null, Payment = null };

    public CheckoutDraft WithShipping(CheckoutShippingOptionDto option) => this with { Shipping = option, Payment = null };

    public CheckoutDraft Advance() => this with { Revision = checked(Revision + 1) };
}
