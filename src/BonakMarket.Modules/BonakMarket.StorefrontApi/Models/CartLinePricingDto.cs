#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CartLinePricingDto(string CurrencyCode, decimal UnitPrice, decimal? RegularUnitPrice,
    decimal LineTotal, decimal DiscountAmount, bool TaxInclusive);
