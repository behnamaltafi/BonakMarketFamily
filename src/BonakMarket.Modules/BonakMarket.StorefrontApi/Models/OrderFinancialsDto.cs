#nullable enable
namespace BonakMarket.StorefrontApi.Models;

/// <summary>Stored native monetary amounts in order base currency, not today's customer currency.</summary>
public sealed record OrderFinancialsDto(decimal SubtotalInclTax, decimal SubtotalExclTax,
    decimal SubtotalDiscountInclTax, decimal SubtotalDiscountExclTax,
    decimal ShippingInclTax, decimal ShippingExclTax, decimal PaymentFeeInclTax, decimal PaymentFeeExclTax,
    decimal Tax, decimal OrderDiscount, decimal CreditBalance, decimal GiftCardAmount,
    decimal Rounding, decimal Total, decimal RefundedAmount, string? CustomerCurrencyCode, decimal CurrencyRate);
