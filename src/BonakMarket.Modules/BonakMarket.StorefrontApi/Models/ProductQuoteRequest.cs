#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class ProductQuoteRequest
{
    [Range(1, StorefrontLimits.MaxQuantity)] public int Quantity { get; set; } = 1;
    [MaxLength(StorefrontLimits.MaxVariantChoices)] public List<VariantChoice>? Variants { get; set; } = [];
}
