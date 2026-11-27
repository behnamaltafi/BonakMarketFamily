#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record StorefrontBootstrapDto(int StoreId, string StoreName, string? LogoUrl,
    StorefrontLanguageDto Language, StorefrontCurrencyDto WorkingCurrency, StorefrontCurrencyDto BaseCurrency,
    StorefrontFeaturesDto Features);
