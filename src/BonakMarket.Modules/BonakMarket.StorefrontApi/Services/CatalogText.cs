using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Search;

namespace BonakMarket.StorefrontApi.Services;

internal static class CatalogText
{
    // Same normalization in request and SQL expressions. Do not modify stored data.
    internal static string Normalize(string value) => (value ?? string.Empty).Trim()
        .Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "");

    // Preserve native parsing for advanced terms and non-Persian text.
    internal static bool UseNormalizedSearch(string term) => !string.IsNullOrEmpty(term) &&
        term.Any(c => c >= '\u0600' && c <= '\u06ff' && char.IsLetter(c)) &&
        term.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '\u200c');

    internal static IQueryable<Product> ApplyNormalizedSearch(IQueryable<Product> source,
        IQueryable<LocalizedProperty> translations, string term, string[] fields, SearchMode mode, int languageId)
    {
        var text = Normalize(term);
        var name = fields.Contains(CatalogSearchQuery.KnownFilters.Name);
        var sku = fields.Contains(CatalogSearchQuery.KnownFilters.Sku);
        var description = fields.Contains(CatalogSearchQuery.KnownFilters.ShortDescription);

        // Use SQL-translatable expressions instead of normalizing materialized rows.
        // The same mode and field settings apply to stored values and current-language translations.
        var localized = translations.Where(x => x.LanguageId == languageId && x.LocaleKeyGroup == nameof(Product) &&
            ((name && x.LocaleKey == nameof(Product.Name)) || (description && x.LocaleKey == nameof(Product.ShortDescription))))
            .Select(x => new { x.EntityId, Value = (x.LocaleValue ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "") });
        var values = source.Select(x => new
        {
            Product = x,
            Name = (x.Name ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", ""),
            Sku = (x.Sku ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", ""),
            Description = (x.ShortDescription ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "")
        });
        return mode switch
        {
            SearchMode.ExactMatch => values.Where(x => (name && x.Name == text) || (sku && x.Sku == text) ||
                (description && x.Description == text) || localized.Any(t => t.EntityId == x.Product.Id && t.Value == text)).Select(x => x.Product),
            SearchMode.StartsWith => values.Where(x => (name && x.Name.StartsWith(text)) || (sku && x.Sku.StartsWith(text)) ||
                (description && x.Description.StartsWith(text)) || localized.Any(t => t.EntityId == x.Product.Id && t.Value.StartsWith(text))).Select(x => x.Product),
            _ => values.Where(x => (name && x.Name.Contains(text)) || (sku && x.Sku.Contains(text)) ||
                (description && x.Description.Contains(text)) || localized.Any(t => t.EntityId == x.Product.Id && t.Value.Contains(text))).Select(x => x.Product)
        };
    }
}
