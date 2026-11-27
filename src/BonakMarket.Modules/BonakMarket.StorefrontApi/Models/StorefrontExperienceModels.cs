#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record StorefrontLanguageDto(int Id, string Name, string Culture, string Direction);
public sealed record StorefrontCurrencyDto(string Code, int DecimalPlaces);
public sealed record StorefrontFeaturesDto(bool CouponEnabled, bool GiftCardEnabled, bool RewardPointsEnabled,
    bool ActivatableCartItems, bool ReturnRequestsEnabled, bool StockSubscriptionsEnabled,
    bool NativeCheckoutConfirmationEnabled, bool NativeOnlinePaymentEnabled)
{
    public bool ShowBestsellersOnHomepage { get; init; }
    public int NumberOfBestsellersOnHomepage { get; init; }
}
public sealed record StorefrontBootstrapDto(int StoreId, string StoreName, string? LogoUrl,
    StorefrontLanguageDto Language, StorefrontCurrencyDto WorkingCurrency, StorefrontCurrencyDto BaseCurrency,
    StorefrontFeaturesDto Features);
public sealed record CustomerSummaryDto(int? CartQuantity, int? WishlistQuantity, int? OrderCount,
    int? ReturnCount, int? StockSubscriptionCount, int? RewardPointsBalance, bool ProfileRequired);
public sealed record TopicSummaryDto(int Id, string? SystemName, string? Title, string? Intro);
public sealed record TopicDetailDto(int Id, string? SystemName, string? Title, string? Intro, string? BodyHtml,
    string? MetaTitle, string? MetaDescription);
public sealed record MyReviewDto(int Id, int ProductId, string? CurrentProductName, string Title,
    string ReviewText, int Rating, bool Approved, DateTime CreatedOnUtc);

public sealed record CatalogFacetValueDto(int Id, string Name, int ProductCount);
public sealed record CatalogFacetsDto(int MatchingProducts, IReadOnlyList<CatalogFacetValueDto> Categories,
    IReadOnlyList<CatalogFacetValueDto> Brands, bool CategoriesTruncated, bool BrandsTruncated)
{
    public bool CanFilterPrice { get; init; }
    public string? PriceFilterCurrencyCode { get; init; }
    public CatalogPriceRangeDto? PriceRange { get; init; }
    public CatalogAvailabilityDto Availability { get; init; } = new(0, 0);
}
public sealed record CatalogPriceRangeDto(decimal Min, decimal Max, string CurrencyCode);
public sealed record CatalogAvailabilityDto(int InStock, int OutOfStock);
