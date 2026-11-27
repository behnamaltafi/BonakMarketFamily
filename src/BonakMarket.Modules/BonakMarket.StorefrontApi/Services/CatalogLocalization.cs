using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;

namespace BonakMarket.StorefrontApi.Services;

internal static class CatalogLocalization
{
    internal static async Task<Dictionary<(int EntityId, string LocaleKey), string>> LoadAsync(
        SmartDbContext db, int[] ids, int languageId, string entityName, string[] keys, CancellationToken ct)
    {
        var rows = await db.LocalizedProperties.AsNoTracking()
            .Where(x => ids.Contains(x.EntityId) && x.LanguageId == languageId &&
                x.LocaleKeyGroup == entityName && keys.Contains(x.LocaleKey))
            .OrderBy(x => x.Id).Select(x => new { x.EntityId, x.LocaleKey, x.LocaleValue }).ToListAsync(ct);
        return rows.GroupBy(x => (x.EntityId, x.LocaleKey))
            .ToDictionary(x => x.Key, x => x.Select(y => y.LocaleValue).FirstOrDefault(y => !string.IsNullOrWhiteSpace(y)));
    }

    internal static string ValueOrFallback(Dictionary<(int EntityId, string LocaleKey), string> labels,
        int id, string key, string fallback) =>
        labels.TryGetValue((id, key), out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
}
