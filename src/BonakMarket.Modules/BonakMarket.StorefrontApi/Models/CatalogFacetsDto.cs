#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CatalogFacetsDto(int MatchingProducts, IReadOnlyList<CatalogFacetValueDto> Categories,
    IReadOnlyList<CatalogFacetValueDto> Brands, bool CategoriesTruncated, bool BrandsTruncated)
{
    public bool CanFilterPrice { get; init; }
    public string? PriceFilterCurrencyCode { get; init; }
    public CatalogPriceRangeDto? PriceRange { get; init; }
    public CatalogAvailabilityDto Availability { get; init; } = new(0, 0);
}
