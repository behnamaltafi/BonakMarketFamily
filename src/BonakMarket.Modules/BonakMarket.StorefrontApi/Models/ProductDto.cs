#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Sku { get; set; }
    public int? MediaFileId { get; set; }
    public string? ImageUrl { get; set; }
    public PriceDto? Price { get; set; }
    // Availability describes sellability, not an inventory reservation.
    public string Availability { get; set; } = "unavailable";
    public int MinimumQuantity { get; set; }
    public int MaximumQuantity { get; set; }
    public int QuantityStep { get; set; }
    public IReadOnlyList<int> AllowedQuantities { get; set; } = [];
    public QuantityUnitDto? QuantityUnit { get; set; }
    public int? UnitsPerPackage { get; set; }
    /// <summary>Derived retail price per individual item, only for an explicitly configured package sale unit.</summary>
    public decimal? ConsumerPrice { get; set; }
    public string? ConsumerPriceCurrencyCode { get; set; }
    public bool RequiresConfiguration { get; set; }
    public bool CanAddToCart { get; set; }
    public bool? HasDiscount { get; set; }
    public bool IsPreOrder { get; set; }
}
