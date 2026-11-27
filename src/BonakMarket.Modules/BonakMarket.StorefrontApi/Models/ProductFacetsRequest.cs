#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Product filters only; facets do not depend on pagination, sorting or price projection.</summary>
public sealed class ProductFacetsRequest : IValidatableObject
{
    [StringLength(StorefrontLimits.MaxSearchLength)] public string? Search { get; set; }
    [Range(1, int.MaxValue)] public int? CategoryId { get; set; }
    [Range(1, int.MaxValue)] public int? BrandId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStock { get; set; }
    public bool? HasDiscount { get; set; }
    public bool? ShowOnHomePage { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        ToSearchRequest().Validate(context);

    internal ProductSearchRequest ToSearchRequest() => new()
    {
        Search = Search, CategoryId = CategoryId, BrandId = BrandId,
        MinPrice = MinPrice, MaxPrice = MaxPrice, InStock = InStock,
        HasDiscount = HasDiscount, ShowOnHomePage = ShowOnHomePage, IncludePrice = false
    };
}
