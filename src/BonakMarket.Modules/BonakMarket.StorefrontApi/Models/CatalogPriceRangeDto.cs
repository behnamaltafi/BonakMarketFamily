#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CatalogPriceRangeDto(decimal Min, decimal Max, string CurrencyCode);
