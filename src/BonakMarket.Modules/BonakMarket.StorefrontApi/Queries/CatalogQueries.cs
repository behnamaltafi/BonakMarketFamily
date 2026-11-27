using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Queries;

public sealed class CatalogQueries(SmartDbContext db, StorefrontRequestContext request, IMediaService media, MediaSettings mediaSettings)
{
    public async Task<PageDto<CategoryDto>> CategoriesAsync(CategorySearchRequest filter, CancellationToken ct)
    {
        var query = StorefrontScope.Categories(db, request.Customer, request.StoreId).AsNoTracking();
        if (filter.ParentId.HasValue)
            query = filter.ParentId == 0 ? query.Where(x => x.ParentId == null) : query.Where(x => x.ParentId == filter.ParentId);
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Name.Contains(filter.Search));
        var page = await query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new CategoryDto { Id = x.Id, ParentId = x.ParentId, DisplayOrder = x.DisplayOrder,
                Name = x.Name, MediaFileId = x.MediaFileId }).PageAsync(filter, ct);

        var urls = await ImageUrlsAsync(page.Items.Select(x => x.MediaFileId), mediaSettings.CategoryThumbPictureSize, ct);
        foreach (var item in page.Items)
            item.ImageUrl = item.MediaFileId is int id && urls.TryGetValue(id, out var url) ? url : null;
        return page;
    }

    public async Task<PageDto<BrandDto>> BrandsAsync(BrandSearchRequest filter, CancellationToken ct)
    {
        var query = StorefrontScope.Brands(db, request.Customer, request.StoreId).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Name.Contains(filter.Search));
        var page = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new BrandDto { Id = x.Id, Name = x.Name, MediaFileId = x.MediaFileId }).PageAsync(filter, ct);

        var urls = await ImageUrlsAsync(page.Items.Select(x => x.MediaFileId), mediaSettings.ManufacturerThumbPictureSize, ct);
        foreach (var item in page.Items)
            item.ImageUrl = item.MediaFileId is int id && urls.TryGetValue(id, out var url) ? url : null;
        return page;
    }

    // Resolve only media linked to the already filtered page, in one batch rather than one query per row.
    // Preserve the native media URL generator and thumbnail settings; no placeholder for missing media.
    private async Task<Dictionary<int, string>> ImageUrlsAsync(IEnumerable<int?> mediaIds, int thumbnailSize, CancellationToken ct)
    {
        var ids = mediaIds.Where(x => x > 0).Select(x => x.Value).Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<int, string>();

        ct.ThrowIfCancellationRequested();
        var files = await media.GetFilesByIdsAsync(ids, MediaLoadFlags.AsNoTracking);
        ct.ThrowIfCancellationRequested();
        return files.Where(x => !x.Deleted && !x.IsTransient)
            .ToDictionary(x => x.Id, x => media.GetUrl(x, thumbnailSize, doFallback: false));
    }
}
