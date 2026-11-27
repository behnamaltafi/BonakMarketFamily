#nullable enable
namespace BonakMarket.StorefrontApi.Models;

/// <summary>Native primary currency. Subtotal already reflects product-level discounts.</summary>
public sealed record CartSummaryDto(int ItemCount, int TotalQuantity, int ActiveQuantity, string? CurrencyCode,
    decimal? SubTotal, decimal? SubTotalDiscountAmount, decimal? SubTotalAfterDiscount,
    decimal? OrderDiscountAmount, decimal? ShippingAmount, decimal? TaxAmount, bool? TaxInclusive,
    decimal? GiftCardAmount, decimal? RewardPointsAmount, int? RedeemedRewardPoints, decimal? CreditBalanceAmount,
    decimal? EstimatedTotal, decimal? PayableTotal);
