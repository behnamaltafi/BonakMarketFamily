using Microsoft.EntityFrameworkCore;
using Autofac;
using BonakMarket.Core;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Catalog.Pricing;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class CatalogFacade(
    SmartDbContext db, ICatalogSearchService search, IPriceCalculationService prices,
    IMediaService media, IPermissionService permissions, IWorkContext work,
    IStoreContext stores, IComponentContext components, StorefrontRequestContext request)
{
    public Task<bool> CanDisplayPriceAsync() => permissions.AuthorizeAsync(Permissions.Catalog.DisplayPrice, request.Customer);

    public async Task<PageDto<ProductDto>> SearchAsync(ProductSearchRequest filter, CancellationToken ct)
    {
        var term = filter.Search?.Trim();
        var query = string.IsNullOrEmpty(term) ? new CatalogSearchQuery() :
            new CatalogSearchQuery(new[] { CatalogSearchQuery.KnownFilters.Name, CatalogSearchQuery.KnownFilters.Sku,
                CatalogSearchQuery.KnownFilters.ShortDescription }, term);
        query.VisibleOnly(request.Customer).HasStoreId(request.StoreId)
            .WithLanguage(work.WorkingLanguage).WithCurrency(work.WorkingCurrency);
        if (filter.CategoryId.HasValue)
        {
            var category = await StorefrontScope.Categories(db, request.Customer, request.StoreId)
                .AsNoTracking().FirstOrDefaultAsync(x => x.Id == filter.CategoryId.Value, ct);
            if (category == null) return Empty(filter);
            query.WithCategoryTreePath(category.TreePath, null);
        }
        if (filter.BrandId.HasValue)
        {
            if (!await StorefrontScope.Brands(db, request.Customer, request.StoreId).AnyAsync(x => x.Id == filter.BrandId.Value, ct))
                return Empty(filter);
            query.WithManufacturerIds(null, filter.BrandId.Value);
        }
        if (filter.MinPrice.HasValue || filter.MaxPrice.HasValue)
            query.PriceBetween(filter.MinPrice, filter.MaxPrice);
        if (filter.InStock.HasValue) query.AvailableOnly(filter.InStock.Value);
        if (filter.HasDiscount.HasValue) query.HasDiscount(filter.HasDiscount.Value);
        query.SortBy(filter.Sort switch
        {
            CatalogSort.NameAsc => ProductSortingEnum.NameAsc,
            CatalogSort.NameDesc => ProductSortingEnum.NameDesc,
            CatalogSort.PriceAsc => ProductSortingEnum.PriceAsc,
            CatalogSort.PriceDesc => ProductSortingEnum.PriceDesc,
            CatalogSort.Newest => ProductSortingEnum.CreatedOn,
            _ => ProductSortingEnum.Initial
        });

        // Native LINQ search with a strictly scoped base query. Unlike an external index,
        // the result count cannot contain stale/unpublished or cross-store product IDs.
        var source = search.PrepareQuery(query, StorefrontScope.Products(db, request.Customer, request.StoreId,
            forSearch: !string.IsNullOrEmpty(term)).AsNoTracking());
        var page = await source.ToPagedList(filter.Page - 1, filter.PageSize).LoadAsync(cancelToken: ct);
        var items = await MapAsync(page.ToList(), filter.IncludePrice);
        return NativePaging.Map(page, items);
    }

    public async Task<ProductDto> FindAsync(int id, CancellationToken ct)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forSearch: true)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product == null) return null;
        return (await MapAsync(new[] { product }, true, false))[0];
    }

    public async Task<PriceDto> PriceAsync(int id, int quantity, CancellationToken ct)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forSearch: true)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product == null || product.CallForPrice || product.CustomerEntersPrice) return null;
        var options = prices.CreateDefaultOptions(false, request.Customer);
        return await Price(product, quantity, options);
    }

    private async Task<List<ProductDto>> MapAsync(IList<Product> products, bool includePrice, bool forListing = true)
    {
        var allowed = includePrice && await CanDisplayPriceAsync();
        var batch = new ProductBatchContext(products, db, components, stores.CurrentStore, request.Customer,
            includeHidden: false, loadMainMediaOnly: true);
        var options = allowed ? prices.CreateDefaultOptions(forListing, request.Customer, batchContext: batch) : null;
        var ids = products.Select(x => x.Id).ToArray();
        var pictures = await db.Set<ProductMediaFile>().AsNoTracking()
            .Where(x => ids.Contains(x.ProductId) && !x.MediaFile.Deleted && !x.MediaFile.Hidden && !x.MediaFile.IsTransient)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new { x.ProductId, x.MediaFileId }).ToListAsync();
        var lookup = pictures.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First().MediaFileId);
        var items = new List<ProductDto>(products.Count);
        foreach (var product in products)
        {
            var item = new ProductDto { Id = product.Id, Name = product.Name, ShortDescription = product.ShortDescription };
            if (lookup.TryGetValue(product.Id, out var picture))
            {
                item.MediaFileId = picture;
                item.ImageUrl = await media.GetUrlAsync(picture, 400, doFallback: false);
            }
            if (allowed && !product.CallForPrice && !product.CustomerEntersPrice)
                item.Price = await Price(product, 1, options);
            items.Add(item);
        }
        return items;
    }

    private async Task<PriceDto> Price(Product product, int quantity, PriceCalculationOptions options)
    {
        var result = await prices.CalculatePriceAsync(new PriceCalculationContext(product, quantity, options));
        return new PriceDto(product.Id, result.FinalPrice.Currency.CurrencyCode, result.FinalPrice.Amount,
            result.RegularPrice?.Amount, result.DiscountAmount.Amount);
    }

    private static PageDto<ProductDto> Empty(PageRequest page) =>
        new(Array.Empty<ProductDto>(), page.Page, page.PageSize, 0, 0);
}
