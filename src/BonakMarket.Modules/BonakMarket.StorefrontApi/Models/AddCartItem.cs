#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class AddCartItem
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, StorefrontLimits.MaxQuantity)] public int Quantity { get; set; } = 1;
    [MaxLength(StorefrontLimits.MaxVariantChoices)] public List<VariantChoice> Variants { get; set; } = new();
}
