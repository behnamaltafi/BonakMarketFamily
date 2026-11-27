using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;

namespace BonakMarket.StorefrontApi.Services;

internal static class CatalogMedia
{
    internal static async Task<Dictionary<int, int>> MainPictureIdsAsync(SmartDbContext db, int[] productIds, CancellationToken ct)
    {
        var pictures = await db.Set<ProductMediaFile>().AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId) && !x.MediaFile.Deleted && !x.MediaFile.Hidden && !x.MediaFile.IsTransient)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new { x.ProductId, x.MediaFileId }).ToListAsync(ct);
        return pictures.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First().MediaFileId);
    }

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
