#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ProductQuotePricingDto(string CurrencyCode, decimal UnitPrice, decimal? RegularUnitPrice,
    decimal LineTotal, decimal DiscountAmount, bool TaxInclusive);
