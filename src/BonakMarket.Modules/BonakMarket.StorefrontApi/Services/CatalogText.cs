namespace BonakMarket.StorefrontApi.Services;

internal static class CatalogText
{
    // Same normalization in request and SQL expressions. Do not modify stored data.
    internal static string Normalize(string value) => (value ?? string.Empty).Trim()
        .Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "");
}
