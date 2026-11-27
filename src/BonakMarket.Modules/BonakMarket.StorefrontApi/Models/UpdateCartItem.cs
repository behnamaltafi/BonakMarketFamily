#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class UpdateCartItem : IValidatableObject
{
    [Range(1, StorefrontLimits.MaxQuantity)] public int? Quantity { get; set; }
    public bool? Active { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!Quantity.HasValue && !Active.HasValue)
            yield return new ValidationResult("Quantity or active is required.", new[] { nameof(Quantity), nameof(Active) });
    }
}
