#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class VariantChoice
{
    [Range(1, int.MaxValue)] public int VariantAttributeId { get; set; }
    [Range(1, int.MaxValue)] public int ValueId { get; set; }
}
