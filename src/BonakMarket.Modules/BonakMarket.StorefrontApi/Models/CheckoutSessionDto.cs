#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Resumable, provisional checkout selections. Never a payment authorization or a reservation.</summary>
public sealed record CheckoutSessionDto(Guid Id, int Revision, string CartRevision, string Status,
    string CurrentStep, IReadOnlyList<string> CompletedSteps, DateTime ExpiresAt,
    bool BillingRequired, bool ShippingRequired, bool PaymentSelectionRequired,
    int? BillingAddressId, int? ShippingAddressId, int? DefaultBillingAddressId, int? DefaultShippingAddressId,
    CheckoutShippingOptionDto? ShippingMethod, CheckoutPaymentOptionDto? PaymentMethod,
    CartSummaryDto CartPreview, bool ReadyForReview, bool CanConfirm, IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings, bool RequiresBrowserSession, string BrowserCheckoutUrl);
