using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CheckoutReadinessDto(bool CanStart, string[] Blockers, bool ShippingRequired,
    int? DefaultBillingAddressId, int? DefaultShippingAddressId, string BrowserCheckoutUrl, bool RequiresBrowserSession);
public sealed record CheckoutShippingOptionDto(int Id, string Provider, string Name, string Description, decimal Rate);
public sealed record CheckoutPaymentMethodDto(string SystemName, bool RequiresInteraction, bool RequiresConfirmation);
public sealed record OrderBrowserActionsDto(string InvoiceUrl, string PaymentPageUrl, bool CanRetryPayment, bool RequiresBrowserSession);
public sealed class ReviewRequest
{
    [Required, StringLength(200)] public string Title { get; set; }
    [Required, StringLength(10000)] public string ReviewText { get; set; }
    [Range(1, 5)] public int Rating { get; set; }
    public bool GdprConsent { get; set; }
}
public sealed class ReviewVoteRequest
{
    public bool WasHelpful { get; set; }
}
public sealed record ReviewResultDto(int Id, bool Approved);
public sealed record ReviewVoteDto(int HelpfulYes, int HelpfulNo);
