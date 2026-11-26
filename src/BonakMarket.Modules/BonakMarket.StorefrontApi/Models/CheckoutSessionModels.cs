#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class CheckoutAddressesRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    [Range(1, int.MaxValue)] public int? BillingAddressId { get; set; }
    [Range(1, int.MaxValue)] public int? ShippingAddressId { get; set; }
}
public sealed class CheckoutShippingSelectionRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    [Range(0, int.MaxValue)] public int ShippingMethodId { get; set; }
    [Required, StringLength(200)] public string Provider { get; set; } = "";
    // The observed provider rate is a precondition, not a client-supplied price.
    [Required] public decimal? ObservedRate { get; set; }
}
public sealed class CheckoutPaymentSelectionRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    [Required, StringLength(200)] public string SystemName { get; set; } = "";
}

public sealed record CheckoutPaymentOptionDto(string SystemName, string Name, string? Description,
    bool RequiresInteraction, bool RequiresConfirmation, bool RequiresPaymentSelection);

/// <summary>Resumable, provisional checkout selections. Never a payment authorization or a reservation.</summary>
public sealed record CheckoutSessionDto(Guid Id, int Revision, string CartRevision, string Status,
    string CurrentStep, IReadOnlyList<string> CompletedSteps, DateTime ExpiresAt,
    bool BillingRequired, bool ShippingRequired, bool PaymentSelectionRequired,
    int? BillingAddressId, int? ShippingAddressId, int? DefaultBillingAddressId, int? DefaultShippingAddressId,
    CheckoutShippingOptionDto? ShippingMethod, CheckoutPaymentOptionDto? PaymentMethod,
    CartSummaryDto CartPreview, bool ReadyForReview, bool CanConfirm, IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings, bool RequiresBrowserSession, string BrowserCheckoutUrl);
