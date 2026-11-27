#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed record PriceDto(int ProductId, string CurrencyCode, decimal FinalPrice, decimal? RegularPrice, decimal DiscountAmount)
{
    /// <summary>Number of units used to calculate the displayed unit price.</summary>
    public int PricingQuantity { get; init; } = 1;
    /// <summary>Native retail/MSRP per sellable unit, in the same currency and tax basis as FinalPrice.</summary>
    public decimal? RetailPrice { get; init; }
    public bool TaxInclusive { get; init; }
    /// <summary>Native saving relative to the regular price or retail price, not an applied discount rule.</summary>
    public decimal? SavingPercentage { get; init; }
    public decimal? SavingAmount { get; init; }
    public string SavingReference { get; init; }
    /// <summary>Native saving against regular price only; null for an MSRP-only comparison.</summary>
    public decimal? DiscountPercentage => SavingReference == "regular_price" ? SavingPercentage : null;
}
