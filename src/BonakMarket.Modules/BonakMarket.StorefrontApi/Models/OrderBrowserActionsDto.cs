using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record OrderBrowserActionsDto(string InvoiceUrl, string PaymentPageUrl, bool CanRetryPayment, bool RequiresBrowserSession);
