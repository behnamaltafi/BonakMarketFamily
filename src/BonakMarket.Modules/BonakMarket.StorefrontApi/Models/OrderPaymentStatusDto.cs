#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record OrderPaymentStatusDto(int OrderId, OrderStatusDto OrderStatus, OrderStatusDto PaymentStatus,
    bool IsPaid, DateTime? PaidDateUtc, DateTime UpdatedOnUtc, decimal OrderTotal, decimal RefundedAmount,
    string? CustomerCurrencyCode, decimal CurrencyRate, bool CanRetryPayment,
    bool RequiresBrowserSession, string? BrowserPaymentUrl);
