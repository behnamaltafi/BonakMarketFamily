#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record StorefrontLanguageDto(int Id, string Name, string Culture, string Direction);
public sealed record StorefrontCurrencyDto(string Code, int DecimalPlaces);
public sealed record StorefrontFeaturesDto(bool CouponEnabled, bool GiftCardEnabled, bool RewardPointsEnabled,
    bool ActivatableCartItems, bool ReturnRequestsEnabled, bool StockSubscriptionsEnabled,
    bool NativeCheckoutConfirmationEnabled, bool NativeOnlinePaymentEnabled);
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
