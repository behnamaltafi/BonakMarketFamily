using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Autofac;
using BonakMarket.Core;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Catalog.Categories;
using BonakMarket.Core.Catalog.Brands;
using BonakMarket.Core.Catalog;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Common;
using BonakMarket.Core.Catalog.Pricing;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Configuration;
using BonakMarket.StorefrontApi.Configuration;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Services;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Queries;

internal sealed class ProductCatalogQueries(SmartDbContext db, ICatalogSearchService search,
    IWorkContext work, CatalogSettings catalogSettings, StorefrontRequestContext request)
{
    // Only these three term fields are supported by the native LINQ visitor.
    // Use the same configured fields in all three search endpoints.
    internal static string[] SearchFields(SearchSettings settings) =>
        settings.GetSearchFields(false).Where(x => x == CatalogSearchQuery.KnownFilters.Name ||
            x == CatalogSearchQuery.KnownFilters.Sku || x == CatalogSearchQuery.KnownFilters.ShortDescription)
            .Distinct().ToArray();

    internal static bool? EffectiveStockFilter(ProductFilterRequest filter, SearchSettings settings) =>
        filter.InStock ?? (!string.IsNullOrWhiteSpace(filter.Search) && !settings.IncludeNotAvailable ? true : (bool?)null);

    internal static IQueryable<Product> ApplyStockFilter(IQueryable<Product> source, bool? inStock)
    {
        if (!inStock.HasValue) return source;
        // The current native AvailableOnly(false) visitor also selects available products.
        // Use its sellability predicate for both values, without changing the Core visitor.
        return source.Where(x => (
            x.ManageInventoryMethodId == (int)ManageInventoryMethod.DontManageStock ||
            (x.ManageInventoryMethodId == (int)ManageInventoryMethod.ManageStock &&
                (x.StockQuantity > 0 || x.BackorderModeId != (int)BackorderMode.NoBackorders)) ||
            (x.ManageInventoryMethodId == (int)ManageInventoryMethod.ManageStockByAttributes &&
                x.ProductVariantAttributeCombinations.Any(v => v.IsActive && (v.StockQuantity > 0 || v.AllowOutOfStockOrders)))
            ) == inStock.Value);
    }

    internal async Task<IQueryable<Product>> SearchSourceAsync(ProductFilterRequest filter, CancellationToken ct, SearchSettings settings)
    {
        settings ??= new SearchSettings();
        var term = filter.Search?.Trim();
        var hasTerm = !string.IsNullOrEmpty(term);
        var normalized = CatalogText.UseNormalizedSearch(term);
        var query = hasTerm && !normalized
            ? new CatalogSearchQuery(SearchFields(settings), term, settings.SearchMode)
            : new CatalogSearchQuery();
        if (hasTerm) query.OriginatesFrom(CatalogSearchQuery.KnownOrigins.Search);
        query.VisibleOnly(request.Customer).HasStoreId(request.StoreId)
            .WithLanguage(work.WorkingLanguage).WithCurrency(work.WorkingCurrency);
        IQueryable<Product> scoped = StorefrontScope.Products(db, request.Customer, request.StoreId, forSearch: hasTerm).AsNoTracking();
        if (normalized)
            scoped = CatalogText.ApplyNormalizedSearch(scoped, db.LocalizedProperties.AsNoTracking(), term,
                SearchFields(settings), settings.SearchMode, work.WorkingLanguage.Id);
        if (filter.CategoryId.HasValue)
        {
            var visible = StorefrontScope.Categories(db, request.Customer, request.StoreId).AsNoTracking();
            var category = await visible.FirstOrDefaultAsync(x => x.Id == filter.CategoryId.Value, ct);
            if (category == null) return db.Set<Product>().AsNoTracking().Where(x => false);
            var ids = catalogSettings.ShowProductsFromSubcategories && !string.IsNullOrEmpty(category.TreePath)
                ? visible.Where(x => x.TreePath.StartsWith(category.TreePath)).Select(x => x.Id)
                : visible.Where(x => x.Id == category.Id).Select(x => x.Id);
            scoped = scoped.Where(p => p.ProductCategories.Any(c => !c.IsSystemMapping && ids.Contains(c.CategoryId)));
            if (catalogSettings.ShowProductsFromSubcategories && !string.IsNullOrEmpty(category.TreePath))
                query.WithCategoryTreePath(category.TreePath, null);
            else query.WithCategoryIds(null, category.Id);
        }
        if (filter.BrandId.HasValue)
        {
            if (!await StorefrontScope.Brands(db, request.Customer, request.StoreId).AnyAsync(x => x.Id == filter.BrandId.Value, ct))
                return db.Set<Product>().AsNoTracking().Where(x => false);
            query.WithManufacturerIds(null, filter.BrandId.Value);
        }
        if (filter.HasDiscount.HasValue) query.HasDiscount(filter.HasDiscount.Value);
        if (filter.ShowOnHomePage.HasValue) query.HomePageProductsOnly(filter.ShowOnHomePage.Value);
        if (hasTerm && (filter is not ProductSearchRequest sorted || sorted.Sort == CatalogSort.Initial))
            query.SortBy(ProductSortingEnum.Relevance);

        // Native LINQ search with a strictly scoped base query. Unlike an external index,
        // the result count cannot contain stale/unpublished or cross-store product IDs.
        var source = search.PrepareQuery(query, scoped);
        source = ApplyStockFilter(source, EffectiveStockFilter(filter, settings));
        return ApplyCatalogPriceAndSort(source, filter, DateTime.UtcNow);
    }

    internal static IQueryable<Product> ApplyCatalogPriceAndSort(IQueryable<Product> source, ProductFilterRequest filter, DateTime now)
    {
        var sort = (filter as ProductSearchRequest)?.Sort ?? CatalogSort.Initial;
        // Catalog price (base currency, before customer-specific taxes/discounts), not a checkout quote.
        // Keep decimal precision and use a single timestamp for the range and price order.
        if (filter.MinPrice.HasValue || filter.MaxPrice.HasValue ||
            sort is CatalogSort.PriceAsc or CatalogSort.PriceDesc)
        {
            // Do not reveal catalog prices for products whose prices are intentionally unavailable.
            // Price bounds exclude those rows, but sorting alone preserves them at the end.
            var priced = source.Select(x => new { Product = x,
                Price = x.CallForPrice || x.CustomerEntersPrice ? (decimal?)null :
                    (x.SpecialPrice.HasValue &&
                    (!x.SpecialPriceStartDateTimeUtc.HasValue || x.SpecialPriceStartDateTimeUtc.Value <= now) &&
                    (!x.SpecialPriceEndDateTimeUtc.HasValue || x.SpecialPriceEndDateTimeUtc.Value >= now)
                    ? x.SpecialPrice.Value : x.Price) });
            if (filter.MinPrice.HasValue) priced = priced.Where(x => x.Price.HasValue && x.Price.Value >= filter.MinPrice.Value);
            if (filter.MaxPrice.HasValue) priced = priced.Where(x => x.Price.HasValue && x.Price.Value <= filter.MaxPrice.Value);
            if (sort == CatalogSort.PriceAsc)
                return priced.OrderBy(x => !x.Price.HasValue).ThenBy(x => x.Price).ThenBy(x => x.Product.Id).Select(x => x.Product);
            if (sort == CatalogSort.PriceDesc)
                return priced.OrderBy(x => !x.Price.HasValue).ThenByDescending(x => x.Price).ThenBy(x => x.Product.Id).Select(x => x.Product);
            source = priced.Select(x => x.Product);
        }

        // Stable tie-breaking prevents duplicates/skipped products between pages with equal sort values.
        return sort switch
        {
            CatalogSort.NameAsc => source.OrderBy(x => x.Name).ThenBy(x => x.Id),
            CatalogSort.NameDesc => source.OrderByDescending(x => x.Name).ThenBy(x => x.Id),
            CatalogSort.Newest => source.OrderByDescending(x => x.CreatedOnUtc).ThenBy(x => x.Id),
            CatalogSort.Initial when filter.ShowOnHomePage == true => source.OrderBy(x => x.HomePageDisplayOrder).ThenBy(x => x.Id),
            _ => source
        };
    }

    // Conjunctive facets: same visible result set and filters as the list, independent of page.
    // Counts are direct product mappings, not a recursive sum of descendants.
    public async Task<CatalogFacetsDto> FacetsAsync(ProductFacetsRequest filter, CancellationToken ct, SearchSettings settings = null,
        string priceCurrencyCode = null)
    {
        const int limit = 50;
        var products = (await SearchSourceAsync(filter.ToSearchRequest(), ct, settings)).Select(x => x.Id).Distinct();
        var visibleCategories = StorefrontScope.Categories(db, request.Customer, request.StoreId);
        var visibleBrands = StorefrontScope.Brands(db, request.Customer, request.StoreId);
        var categoryCounts = db.Set<ProductCategory>().AsNoTracking()
            .Where(x => products.Contains(x.ProductId) && !x.IsSystemMapping)
            .Select(x => new { Id = x.CategoryId, x.ProductId }).Distinct()
            .GroupBy(x => x.Id).Select(g => new { Id = g.Key, Count = g.Count() });
        var brandCounts = db.Set<ProductManufacturer>().AsNoTracking()
            .Where(x => products.Contains(x.ProductId))
            .Select(x => new { Id = x.ManufacturerId, x.ProductId }).Distinct()
            .GroupBy(x => x.Id).Select(g => new { Id = g.Key, Count = g.Count() });
        var categories = await (from count in categoryCounts
            join category in visibleCategories on count.Id equals category.Id
            orderby count.Count descending, category.Id
            select new CatalogFacetValueDto(category.Id, category.Name, count.Count))
            .Take(limit + 1).ToListAsync(ct);
        var brands = await (from count in brandCounts
            join brand in visibleBrands on count.Id equals brand.Id
            orderby count.Count descending, brand.Id
            select new CatalogFacetValueDto(brand.Id, brand.Name, count.Count))
            .Take(limit + 1).ToListAsync(ct);
        var categoryItems = categories.Take(limit).ToArray();
        var brandItems = brands.Take(limit).ToArray();
        var categoryIds = categoryItems.Select(x => x.Id).ToArray();
        var brandIds = brandItems.Select(x => x.Id).ToArray();
        var languageId = work.WorkingLanguage.Id;
        var translations = await db.LocalizedProperties.AsNoTracking()
            .Where(x => x.LanguageId == languageId && x.LocaleKey == "Name" &&
                ((x.LocaleKeyGroup == nameof(Category) && categoryIds.Contains(x.EntityId)) ||
                 (x.LocaleKeyGroup == nameof(Manufacturer) && brandIds.Contains(x.EntityId))))
            .OrderBy(x => x.Id).Select(x => new { x.LocaleKeyGroup, x.EntityId, x.LocaleValue }).ToListAsync(ct);
        var labels = translations.GroupBy(x => (x.LocaleKeyGroup, x.EntityId))
            .ToDictionary(x => x.Key, x => x.Select(y => y.LocaleValue).FirstOrDefault(y => !string.IsNullOrWhiteSpace(y)));
        CatalogFacetValueDto Localize(CatalogFacetValueDto item, string entity) =>
            labels.TryGetValue((entity, item.Id), out var label) && !string.IsNullOrWhiteSpace(label)
                ? item with { Name = label } : item;
        var matched = db.Set<Product>().AsNoTracking().Where(x => products.Contains(x.Id));
        var total = await matched.CountAsync(ct);
        var inStock = await ApplyStockFilter(matched, true).CountAsync(ct);
        CatalogPriceRangeDto range = null;
        if (priceCurrencyCode != null && total > 0)
        {
            var now = DateTime.UtcNow;
            var pricesQuery = matched.Where(x => !x.CallForPrice && !x.CustomerEntersPrice)
                .Select(x => x.SpecialPrice.HasValue &&
                    (!x.SpecialPriceStartDateTimeUtc.HasValue || x.SpecialPriceStartDateTimeUtc.Value <= now) &&
                    (!x.SpecialPriceEndDateTimeUtc.HasValue || x.SpecialPriceEndDateTimeUtc.Value >= now)
                    ? x.SpecialPrice.Value : x.Price);
            range = await pricesQuery.GroupBy(x => 1)
                .Select(g => new CatalogPriceRangeDto(g.Min(), g.Max(), priceCurrencyCode)).FirstOrDefaultAsync(ct);
        }
        return new(total, categoryItems.Select(x => Localize(x, nameof(Category))).ToArray(),
            brandItems.Select(x => Localize(x, nameof(Manufacturer))).ToArray(), categories.Count > limit, brands.Count > limit)
        {
            Availability = new(inStock, total - inStock),
            PriceRange = range
        };
    }
}
