#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class ProductSearchRequest : ProductFilterRequest
{
    public CatalogSort Sort { get; set; }
    internal bool RequiresPriceSortAccess => RequiresPriceAccess || Sort is CatalogSort.PriceAsc or CatalogSort.PriceDesc;
    public override IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var error in base.Validate(context)) yield return error;
        if (!Enum.IsDefined(Sort)) yield return new ValidationResult("Invalid sort.", new[] { nameof(Sort) });
    }
}
