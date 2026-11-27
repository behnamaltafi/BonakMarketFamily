using BonakMarket.Core.Content.Media;

namespace BonakMarket.StorefrontApi.Services;

internal static class CatalogMedia
{
    internal static async Task<Dictionary<int, string>> UrlsAsync(IMediaService media, IEnumerable<int?> mediaIds,
        int thumbnailSize, CancellationToken ct)
    {
        var ids = mediaIds.Where(x => x > 0).Select(x => x.Value).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, string>();
        ct.ThrowIfCancellationRequested();
        var files = await media.GetFilesByIdsAsync(ids, MediaLoadFlags.AsNoTracking);
        ct.ThrowIfCancellationRequested();
        return files.Where(x => !x.Deleted && !x.Hidden && !x.IsTransient)
            .ToDictionary(x => x.Id, x => media.GetUrl(x, thumbnailSize, doFallback: false));
    }
}
