using Microsoft.EntityFrameworkCore;
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
using BonakMarket.Core.Identity;
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
    IStoreContext stores, IComponentContext components, CatalogSettings catalogSettings, StorefrontRequestContext request,
    IProductAttributeMaterializer attributes, IShoppingCartValidator validator)
{
    public Task<bool> CanDisplayPriceAsync() => permissions.AuthorizeAsync(Permissions.Catalog.DisplayPrice, request.Customer);

    public async Task<PageDto<ProductDto>> SearchAsync(ProductSearchRequest filter, CancellationToken ct, SearchSettings settings = null)
    {
        var source = await SearchSourceAsync(filter, ct, settings);
        var page = await source.ToPagedList(filter.Page - 1, filter.PageSize).LoadAsync(cancelToken: ct);
        var items = await MapAsync(page.ToList(), filter.IncludePrice, ct: ct);
        return NativePaging.Map(page, items);
    }

    internal static (bool Enabled, int MinimumTermLength, int Limit) InstantSearchPolicy(
        InstantSearchRequest filter, SearchSettings settings)
    {
        var limit = Math.Min(filter.Limit ?? 16, Math.Clamp(settings.InstantSearchNumberOfProducts, 0, 16));
        return (settings.InstantSearchEnabled && limit > 0, Math.Clamp(settings.InstantSearchTermMinLength, 1, 200), limit);
    }

    public async Task<InstantSearchDto> InstantSearchAsync(InstantSearchRequest filter, SearchSettings settings, CancellationToken ct)
    {
        var term = filter.Search?.Trim() ?? string.Empty;
        var (enabled, minimum, limit) = InstantSearchPolicy(filter, settings);
        if (!enabled || term.Length < minimum)
            return new(term, enabled, minimum, limit, false, Array.Empty<InstantSearchProductDto>());

        // Use exactly the same term, search settings, visibility and availability as the full results.
        var source = await SearchSourceAsync(new ProductSearchRequest { Search = term }, ct, settings);
        var products = await source.Take(limit + 1).ToListAsync(ct);
        var hasMore = products.Count > limit;
        if (hasMore) products.RemoveAt(limit);

        if (products.Count == 0)
            return new(term, enabled, minimum, limit, false, Array.Empty<InstantSearchProductDto>());

        var mapped = await MapAsync(products, false, includeImages: settings.ShowProductImagesInInstantSearch,
            imageSize: 120, ct: ct, includePurchaseInfo: false);
        var items = mapped.Select(x => new InstantSearchProductDto(x.Id, x.Name,
            x.Sku, x.MediaFileId, x.ImageUrl)).ToArray();
        return new(term, enabled, minimum, limit, hasMore, items);
    }

    // Only these three term fields are supported by the native LINQ visitor.
    // Use the same configured fields in all three search endpoints.
    internal static string[] SearchFields(SearchSettings settings) =>
        settings.GetSearchFields(false).Where(x => x == CatalogSearchQuery.KnownFilters.Name ||
            x == CatalogSearchQuery.KnownFilters.Sku || x == CatalogSearchQuery.KnownFilters.ShortDescription)
            .Distinct().ToArray();

    internal static bool? EffectiveStockFilter(ProductSearchRequest filter, SearchSettings settings) =>
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

    private async Task<IQueryable<Product>> SearchSourceAsync(ProductSearchRequest filter, CancellationToken ct, SearchSettings settings)
    {
        settings ??= new SearchSettings();
        var term = filter.Search?.Trim();
        var hasTerm = !string.IsNullOrEmpty(term);
        var query = hasTerm
            ? new CatalogSearchQuery(SearchFields(settings), term, settings.SearchMode)
                .OriginatesFrom(CatalogSearchQuery.KnownOrigins.Search)
            : new CatalogSearchQuery();
        query.VisibleOnly(request.Customer).HasStoreId(request.StoreId)
            .WithLanguage(work.WorkingLanguage).WithCurrency(work.WorkingCurrency);
        IQueryable<Product> scoped = StorefrontScope.Products(db, request.Customer, request.StoreId, forSearch: hasTerm).AsNoTracking();
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
        if (hasTerm && filter.Sort == CatalogSort.Initial)
            query.SortBy(ProductSortingEnum.Relevance);

        // Native LINQ search with a strictly scoped base query. Unlike an external index,
        // the result count cannot contain stale/unpublished or cross-store product IDs.
        var source = search.PrepareQuery(query, scoped);
        source = ApplyStockFilter(source, EffectiveStockFilter(filter, settings));
        return ApplyCatalogPriceAndSort(source, filter, DateTime.UtcNow);
    }

    internal static IQueryable<Product> ApplyCatalogPriceAndSort(IQueryable<Product> source, ProductSearchRequest filter, DateTime now)
    {
        // Catalog price (base currency, before customer-specific taxes/discounts), not a checkout quote.
        // Keep decimal precision and use a single timestamp for the range and price order.
        if (filter.MinPrice.HasValue || filter.MaxPrice.HasValue ||
            filter.Sort is CatalogSort.PriceAsc or CatalogSort.PriceDesc)
        {
            // Do not reveal catalog prices for products whose prices are intentionally unavailable.
            var priced = source.Where(x => !x.CallForPrice && !x.CustomerEntersPrice).Select(x => new { Product = x,
                Price = x.SpecialPrice.HasValue &&
                    (!x.SpecialPriceStartDateTimeUtc.HasValue || x.SpecialPriceStartDateTimeUtc.Value <= now) &&
                    (!x.SpecialPriceEndDateTimeUtc.HasValue || x.SpecialPriceEndDateTimeUtc.Value >= now)
                    ? x.SpecialPrice.Value : x.Price });
            if (filter.MinPrice.HasValue) priced = priced.Where(x => x.Price >= filter.MinPrice.Value);
            if (filter.MaxPrice.HasValue) priced = priced.Where(x => x.Price <= filter.MaxPrice.Value);
            if (filter.Sort == CatalogSort.PriceAsc)
                return priced.OrderBy(x => x.Price).ThenBy(x => x.Product.Id).Select(x => x.Product);
            if (filter.Sort == CatalogSort.PriceDesc)
                return priced.OrderByDescending(x => x.Price).ThenBy(x => x.Product.Id).Select(x => x.Product);
            source = priced.Select(x => x.Product);
        }

        // Stable tie-breaking prevents duplicates/skipped products between pages with equal sort values.
        return filter.Sort switch
        {
            CatalogSort.NameAsc => source.OrderBy(x => x.Name).ThenBy(x => x.Id),
            CatalogSort.NameDesc => source.OrderByDescending(x => x.Name).ThenBy(x => x.Id),
            CatalogSort.Newest => source.OrderByDescending(x => x.CreatedOnUtc).ThenBy(x => x.Id),
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

    public async Task<ProductDto> FindAsync(int id, CancellationToken ct)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product == null) return null;
        return (await MapAsync(new[] { product }, true, false))[0];
    }

    public async Task<ProductDetailDto> DetailAsync(int id, CancellationToken ct)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AsNoTracking().Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductAttribute)
            .Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductVariantAttributeValues)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product == null) return null;

        var summary = (await MapAsync(new[] { product }, true, false, ct: ct))[0];
        var imageIds = await db.Set<ProductMediaFile>().AsNoTracking()
            .Where(x => x.ProductId == id && !x.MediaFile.Deleted && !x.MediaFile.Hidden && !x.MediaFile.IsTransient)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).Select(x => x.MediaFileId).ToListAsync(ct);
        var imageUrls = await CatalogMedia.UrlsAsync(media, imageIds.Select(x => (int?)x), 800, ct);
        var images = imageIds.Distinct().Where(imageUrls.ContainsKey)
            .Select(id => new ProductImageDto(id, imageUrls[id])).ToList();

        var specifications = await db.Set<ProductSpecificationAttribute>().AsNoTracking()
            .Where(x => x.ProductId == id &&
                (x.ShowOnProductPage ?? x.SpecificationAttributeOption.SpecificationAttribute.ShowOnProductPage))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new ProductSpecificationDto(x.SpecificationAttributeOption.SpecificationAttributeId,
                x.SpecificationAttributeOption.SpecificationAttribute.Name, x.SpecificationAttributeOptionId,
                x.SpecificationAttributeOption.Name)).ToListAsync(ct);

        var supported = ProductSelection.UnsupportedCode(product) == null && summary.MaximumQuantity >= summary.MinimumQuantity;
        var priceAllowed = await CanDisplayPriceAsync();
        var registered = request.Customer.IsRegistered();
        var canWishlist = registered && supported && !product.DisableWishlistButton &&
            await permissions.AuthorizeAsync(StorefrontPermissions.Wishlist, request.Customer) &&
            await permissions.AuthorizeAsync(Permissions.Cart.AccessWishlist, request.Customer);

        return new ProductDetailDto
        {
            Id = summary.Id, Name = summary.Name, Sku = summary.Sku, ShortDescription = summary.ShortDescription,
            ImageUrl = summary.ImageUrl, MediaFileId = summary.MediaFileId, Price = summary.Price,
            Availability = summary.Availability, MinimumQuantity = summary.MinimumQuantity, MaximumQuantity = summary.MaximumQuantity,
            QuantityStep = summary.QuantityStep, AllowedQuantities = summary.AllowedQuantities, QuantityUnit = summary.QuantityUnit,
            RequiresConfiguration = summary.RequiresConfiguration, CanAddToCart = summary.CanAddToCart, HasDiscount = summary.HasDiscount,
            FullDescriptionHtml = product.FullDescription, ProductTypeId = product.ProductTypeId,
            Images = images, Specifications = specifications,
            Variants = product.ProductVariantAttributes.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
                .Select(x => new ProductDetailVariantDto(x.Id, x.ProductAttribute.Name, x.AttributeControlTypeId,
                    x.IsRequired, x.TextPrompt, ProductSelection.SupportsControl(x.AttributeControlType),
                    x.ProductVariantAttributeValues.OrderBy(v => v.DisplayOrder).ThenBy(v => v.Id)
                        .Select(v => new ProductDetailVariantValueDto(v.Id, v.Name, v.IsPreSelected, v.Color)).ToList())).ToList(),
            PurchaseRules = new ProductPurchaseRulesDto(summary.MinimumQuantity, summary.MaximumQuantity, summary.QuantityStep,
                summary.AllowedQuantities, product.HideQuantityControl, summary.RequiresConfiguration),
            AllowedActions = new ProductCapabilitiesDto(supported && priceAllowed, summary.CanAddToCart, canWishlist),
            RequiresShipping = product.IsShippingEnabled, IsPreOrder = product.AvailableForPreOrder,
            ReviewCount = product.ApprovedTotalReviews,
            AverageRating = product.ApprovedTotalReviews > 0
                ? Math.Round((decimal)product.ApprovedRatingSum / product.ApprovedTotalReviews, 2) : null
        };
    }

    public async Task<(int Status, string Code, ProductQuoteDto Quote)> QuoteAsync(int id, ProductQuoteRequest input, CancellationToken ct)
    {
        // Tracked product is required by native attribute validation. Never save this hypothetical item.
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductVariantAttributeValues)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product == null) return (404, "product_not_found", null);
        if (!await CanDisplayPriceAsync()) return (403, "price_access_denied", null);
        var selection = await ProductSelection.CreateAsync(product, input.Variants, attributes);
        if (selection.Status == 400) return (400, selection.Code, null);
        if (selection.Status != 200)
            return (200, null, new(id, input.Quantity, false, selection.Code, null,
                selection.Warnings ?? [], true));

        var item = new ShoppingCartItem
        {
            Product = product, ProductId = id, Customer = request.Customer, CustomerId = request.Customer.Id,
            StoreId = request.StoreId, Quantity = input.Quantity, ShoppingCartType = ShoppingCartType.ShoppingCart,
            RawAttributes = selection.Selection.AsJson()
        };
        var ctx = new AddToCartContext
        {
            Customer = request.Customer, Product = product, StoreId = request.StoreId,
            CartType = ShoppingCartType.ShoppingCart, Quantity = input.Quantity, RawAttributes = item.RawAttributes
        };

        // Quote validates this selection in isolation, not the existing cart or dependent products.
        // Native cart writes remain the final authority for aggregate stock, quantity and permissions.
        var valid = await validator.ValidateAddToCartItemAsync(ctx, item, Array.Empty<OrganizedShoppingCartItem>());
        if (!valid)
            return (200, null, new(id, input.Quantity, false, "product_validation_failed", null,
                ctx.Warnings, true));

        var options = prices.CreateDefaultOptions(false, request.Customer);
        options.ApplyPreselectedAttributes = false;
        var calculation = await prices.CreateCalculationContextAsync(new OrganizedShoppingCartItem(item), options);
        var (unit, subtotal) = await prices.CalculateSubtotalAsync(calculation);
        return (200, null, new(id, input.Quantity, true, null,
            new ProductQuotePricingDto(unit.FinalPrice.Currency.CurrencyCode, unit.FinalPrice.Amount,
                unit.RegularPrice?.Amount, subtotal.FinalPrice.Amount, subtotal.DiscountAmount.Amount, options.TaxInclusive),
            Array.Empty<string>(), true));
    }

    private async Task<List<ProductDto>> MapAsync(IList<Product> products, bool includePrice, bool forListing = true, bool includeImages = true,
        int imageSize = 400, CancellationToken ct = default, bool includePurchaseInfo = true)
    {
        if (products.Count == 0) return new List<ProductDto>();
        var priceAllowed = (includePrice || includePurchaseInfo) && await CanDisplayPriceAsync();
        var allowed = includePrice && priceAllowed;
        var options = allowed ? prices.CreateDefaultOptions(forListing, request.Customer, batchContext:
            new ProductBatchContext(products, db, components, stores.CurrentStore, request.Customer,
                includeHidden: false, loadMainMediaOnly: true)) : null;
        var ids = products.Select(x => x.Id).ToArray();
        var languageId = work.WorkingLanguage.Id;
        var translations = await db.LocalizedProperties.AsNoTracking()
            .Where(x => ids.Contains(x.EntityId) && x.LanguageId == languageId &&
                x.LocaleKeyGroup == nameof(Product) &&
                (x.LocaleKey == nameof(Product.Name) || x.LocaleKey == nameof(Product.ShortDescription)))
            .OrderBy(x => x.Id).Select(x => new { x.EntityId, x.LocaleKey, x.LocaleValue }).ToListAsync(ct);
        var translated = translations.GroupBy(x => (x.EntityId, x.LocaleKey))
            .ToDictionary(x => x.Key, x => x.Select(y => y.LocaleValue).FirstOrDefault(y => !string.IsNullOrWhiteSpace(y)));
        string Localized(int id, string key, string fallback) =>
            translated.TryGetValue((id, key), out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
        var lookup = new Dictionary<int, int>();
        if (includeImages && ids.Length > 0)
        {
            var pictures = await db.Set<ProductMediaFile>().AsNoTracking()
                .Where(x => ids.Contains(x.ProductId) && !x.MediaFile.Deleted && !x.MediaFile.Hidden && !x.MediaFile.IsTransient)
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
                .Select(x => new { x.ProductId, x.MediaFileId }).ToListAsync(ct);
            lookup = pictures.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First().MediaFileId);
        }
        var imageUrls = await CatalogMedia.UrlsAsync(media, lookup.Values.Select(x => (int?)x), imageSize, ct);
        var sellable = new HashSet<int>();
        var variantInfo = new Dictionary<int, (bool HasVariants, bool UnsupportedRequired)>();
        var unitLookup = new Dictionary<int, QuantityUnitDto>();
        QuantityUnitDto defaultUnit = null;
        var canCart = false;
        if (includePurchaseInfo)
        {
            sellable = (await ApplyStockFilter(db.Set<Product>().AsNoTracking().Where(x => ids.Contains(x.Id)), true)
                .Select(x => x.Id).ToListAsync(ct)).ToHashSet();
            var variants = await db.Set<ProductVariantAttribute>().AsNoTracking().Where(x => ids.Contains(x.ProductId))
                .Select(x => new { x.ProductId, x.IsRequired, x.AttributeControlTypeId }).ToListAsync(ct);
            variantInfo = variants.GroupBy(x => x.ProductId).ToDictionary(g => g.Key,
                g => (true, g.Any(x => x.IsRequired && !ProductSelection.SupportsControl((AttributeControlType)x.AttributeControlTypeId))));
            var unitIds = products.Select(x => x.QuantityUnitId).Where(x => x > 0).Select(x => x.Value).Distinct().ToArray();
            var units = await db.Set<QuantityUnit>().AsNoTracking().Where(x => unitIds.Contains(x.Id) || x.IsDefault)
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync(ct);
            var allUnitIds = units.Select(x => x.Id).ToArray();
            var unitTranslations = await db.LocalizedProperties.AsNoTracking().Where(x => x.LanguageId == languageId &&
                x.LocaleKeyGroup == nameof(QuantityUnit) && allUnitIds.Contains(x.EntityId) &&
                (x.LocaleKey == nameof(QuantityUnit.Name) || x.LocaleKey == nameof(QuantityUnit.NamePlural)))
                .OrderBy(x => x.Id).Select(x => new { x.EntityId, x.LocaleKey, x.LocaleValue }).ToListAsync(ct);
            var labels = unitTranslations.GroupBy(x => (x.EntityId, x.LocaleKey))
                .ToDictionary(x => x.Key, x => x.Select(y => y.LocaleValue).FirstOrDefault(y => !string.IsNullOrWhiteSpace(y)));
            string UnitLabel(int id, string key, string fallback) =>
                labels.TryGetValue((id, key), out var name) && !string.IsNullOrWhiteSpace(name) ? name : fallback;
            unitLookup = units.ToDictionary(x => x.Id, x => new QuantityUnitDto(x.Id,
                UnitLabel(x.Id, nameof(QuantityUnit.Name), x.Name), UnitLabel(x.Id, nameof(QuantityUnit.NamePlural), x.NamePlural)));
            var defaultEntity = units.FirstOrDefault(x => x.IsDefault);
            if (defaultEntity != null) defaultUnit = unitLookup[defaultEntity.Id];
            canCart = request.Customer.IsRegistered() &&
                await permissions.AuthorizeAsync(StorefrontPermissions.Cart, request.Customer) &&
                await permissions.AuthorizeAsync(Permissions.Cart.AccessShoppingCart, request.Customer);
        }
        var items = new List<ProductDto>(products.Count);
        foreach (var product in products)
        {
            ct.ThrowIfCancellationRequested();
            var item = new ProductDto { Id = product.Id, Name = Localized(product.Id, nameof(Product.Name), product.Name),
                ShortDescription = Localized(product.Id, nameof(Product.ShortDescription), product.ShortDescription), Sku = product.Sku };
            if (lookup.TryGetValue(product.Id, out var picture))
            {
                item.MediaFileId = picture;
                item.ImageUrl = imageUrls.TryGetValue(picture, out var url) ? url : null;
            }
            if (includePurchaseInfo)
            {
                var quantities = QuantityRules(product);
                var variant = variantInfo.GetValueOrDefault(product.Id);
                var supported = ProductSelection.SupportsProduct(product) && !variant.UnsupportedRequired &&
                    quantities.Max >= quantities.Min;
                item.Availability = sellable.Contains(product.Id) ? "available" : "out_of_stock";
                item.MinimumQuantity = quantities.Min;
                item.MaximumQuantity = quantities.Max;
                item.QuantityStep = Math.Max(1, product.QuantityStep);
                item.AllowedQuantities = quantities.Allowed;
                item.QuantityUnit = product.QuantityUnitId is int unitId && unitLookup.TryGetValue(unitId, out var unit)
                    ? unit : defaultUnit;
                item.RequiresConfiguration = variant.HasVariants || !supported;
                item.CanAddToCart = canCart && supported && sellable.Contains(product.Id);
                item.HasDiscount = priceAllowed ? product.HasDiscountsApplied : null;
                item.IsPreOrder = product.AvailableForPreOrder;
            }
            if (allowed && !product.CallForPrice && !product.CustomerEntersPrice &&
                item.MinimumQuantity > 0 && item.MinimumQuantity <= item.MaximumQuantity)
                item.Price = await Price(product, item.MinimumQuantity, options);
            items.Add(item);
        }
        return items;
    }

    internal static (int Min, int Max, int[] Allowed) QuantityRules(Product product)
    {
        var raw = product.ParseAllowedQuantities();
        var min = Math.Max(1, raw.Length > 0 ? raw[0] : product.OrderMinimumQuantity);
        var max = Math.Min(1000, Math.Max(min, raw.Length > 0 ? raw[^1] : product.OrderMaximumQuantity));
        var allowed = raw.Where(q => q >= min && q <= max).Distinct().ToArray();
        // If there are explicit values, the first valid value is the actual minimum purchase.
        if (raw.Length > 0)
        {
            if (allowed.Length == 0) return (min, 0, allowed);
            min = allowed[0];
            max = allowed[^1];
        }
        return (min, max, allowed);
    }

    private async Task<PriceDto> Price(Product product, int quantity, PriceCalculationOptions options)
    {
        var result = await prices.CalculatePriceAsync(new PriceCalculationContext(product, quantity, options));
        return new PriceDto(product.Id, result.FinalPrice.Currency.CurrencyCode, result.FinalPrice.Amount,
            result.RegularPrice?.Amount, result.DiscountAmount.Amount) { PricingQuantity = quantity };
    }

    private static PageDto<ProductDto> Empty(PageRequest page) =>
        new(Array.Empty<ProductDto>(), page.Page, page.PageSize, 0, 0);
}
