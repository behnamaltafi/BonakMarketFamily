#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public class ProductFilterRequest : PageRequest, IValidatableObject
{
    [StringLength(StorefrontLimits.MaxSearchLength)] public string? Search { get; set; }
    [Range(1, int.MaxValue)] public int? CategoryId { get; set; }
    [Range(1, int.MaxValue)] public int? BrandId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStock { get; set; }
    public bool? HasDiscount { get; set; }
    public bool? ShowOnHomePage { get; set; }
    public bool IncludePrice { get; set; } = true;
    internal bool RequiresPriceAccess => MinPrice.HasValue || MaxPrice.HasValue || HasDiscount.HasValue;
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (MinPrice < 0 || MaxPrice < 0 || MinPrice > MaxPrice)
            yield return new ValidationResult("Invalid price range.", new[] { nameof(MinPrice), nameof(MaxPrice) });
    }
}
