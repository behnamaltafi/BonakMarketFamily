namespace BonakMarket.StorefrontApi.Services;

internal static class WholesaleCardMetadata
{
    internal static int? PackageSize(decimal value) =>
        value > 0m && value <= int.MaxValue && value == decimal.Truncate(value) ? (int)value : null;

    internal static decimal? ConsumerPrice(decimal value) => value > 0m ? value : null;

    internal static string CurrencyCode(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized is { Length: 3 } && normalized.All(c => c >= 'A' && c <= 'Z') ? normalized : null;
    }
}
