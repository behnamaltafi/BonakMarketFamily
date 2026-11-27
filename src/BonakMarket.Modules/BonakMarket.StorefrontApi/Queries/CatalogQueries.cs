using Microsoft.EntityFrameworkCore;
using BonakMarket.Core;
using BonakMarket.Core.Catalog.Brands;
using BonakMarket.Core.Catalog.Categories;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.Core.Localization;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Queries;

public sealed class CatalogQueries(SmartDbContext db, StorefrontRequestContext request, IMediaService media,
    MediaSettings mediaSettings, IWorkContext work)
{
    private IQueryable<LocalizedProperty> LocalizedNames(string entityName)
    {
        var languageId = work.WorkingLanguage.Id;
        return db.LocalizedProperties.AsNoTracking().Where(x => x.LanguageId == languageId &&
            x.LocaleKeyGroup == entityName && x.LocaleKey == "Name" && x.LocaleValue != null && x.LocaleValue != "");
    }

    private IQueryable<CategoryDto> CategoryRows()
    {
        var visible = StorefrontScope.Categories(db, request.Customer, request.StoreId).AsNoTracking();
        var names = LocalizedNames(nameof(Category));
        return visible.Select(x => new CategoryDto
        {
            Id = x.Id, ParentId = x.ParentId, DisplayOrder = x.DisplayOrder, MediaFileId = x.MediaFileId,
            Name = names.Where(n => n.EntityId == x.Id).OrderBy(n => n.Id).Select(n => n.LocaleValue).FirstOrDefault() ?? x.Name,
            HasChildren = visible.Any(c => c.ParentId == x.Id), ShowOnHomePage = x.ShowOnHomePage
        });
    }

    private IQueryable<BrandDto> BrandRows()
    {
        var visible = StorefrontScope.Brands(db, request.Customer, request.StoreId).AsNoTracking();
        var names = LocalizedNames(nameof(Manufacturer));
        return visible.Select(x => new BrandDto
        {
            Id = x.Id, DisplayOrder = x.DisplayOrder, MediaFileId = x.MediaFileId,
            Name = names.Where(n => n.EntityId == x.Id).OrderBy(n => n.Id).Select(n => n.LocaleValue).FirstOrDefault() ?? x.Name
        });
    }

    public async Task<PageDto<CategoryDto>> CategoriesAsync(CategorySearchRequest filter, CancellationToken ct)
    {
        var query = CategoryRows();
        if (filter.ParentId.HasValue)
            query = filter.ParentId == 0 ? query.Where(x => x.ParentId == null) : query.Where(x => x.ParentId == filter.ParentId);
        if (filter.ShowOnHomePage.HasValue) query = query.Where(x => x.ShowOnHomePage == filter.ShowOnHomePage.Value);
        var term = CatalogText.Normalize(filter.Search);
        if (term.Length > 0)
            query = query.Where(x => x.Name.Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term));
        var page = await query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).PageAsync(filter, ct);
        await SetCategoryImagesAsync(page.Items, ct);
        return page;
    }

    public async Task<CategoryDto> CategoryAsync(int id, CancellationToken ct)
    {
        var item = await CategoryRows().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item != null) await SetCategoryImagesAsync(new[] { item }, ct);
        return item;
    }

    public async Task<PageDto<BrandDto>> BrandsAsync(BrandSearchRequest filter, CancellationToken ct)
    {
        var query = BrandRows();
        var term = CatalogText.Normalize(filter.Search);
        if (term.Length > 0)
            query = query.Where(x => x.Name.Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term));
        var page = await query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).PageAsync(filter, ct);
        await SetBrandImagesAsync(page.Items, ct);
        return page;
    }

    public async Task<BrandDto> BrandAsync(int id, CancellationToken ct)
    {
        var item = await BrandRows().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item != null) await SetBrandImagesAsync(new[] { item }, ct);
        return item;
    }

    private async Task SetCategoryImagesAsync(IReadOnlyList<CategoryDto> items, CancellationToken ct)
    {
        var urls = await CatalogMedia.UrlsAsync(media, items.Select(x => x.MediaFileId), mediaSettings.CategoryThumbPictureSize, ct);
        foreach (var item in items)
            item.ImageUrl = item.MediaFileId is int id && urls.TryGetValue(id, out var url) ? url : null;
    }

    private async Task SetBrandImagesAsync(IReadOnlyList<BrandDto> items, CancellationToken ct)
    {
        var urls = await CatalogMedia.UrlsAsync(media, items.Select(x => x.MediaFileId), mediaSettings.ManufacturerThumbPictureSize, ct);
        foreach (var item in items)
            item.ImageUrl = item.MediaFileId is int id && urls.TryGetValue(id, out var url) ? url : null;
    }
}
