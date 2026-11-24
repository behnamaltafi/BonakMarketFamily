using Microsoft.EntityFrameworkCore;
using Autofac;
using BonakMarket.Core;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Catalog.Categories;
using BonakMarket.Core.Catalog.Brands;
using BonakMarket.Core.Catalog;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Search;
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

    public async Task<PageDto<ProductDto>> SearchAsync(ProductSearchRequest filter, CancellationToken ct)
    {
        var source = await SearchSourceAsync(filter, ct);
        var page = await source.ToPagedList(filter.Page - 1, filter.PageSize).LoadAsync(cancelToken: ct);
        var items = await MapAsync(page.ToList(), filter.IncludePrice);
        return NativePaging.Map(page, items);
    }

    private async Task<IQueryable<Product>> SearchSourceAsync(ProductSearchRequest filter, CancellationToken ct)
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
            if (category == null) return db.Set<Product>().AsNoTracking().Where(x => false);
            if (catalogSettings.ShowProductsFromSubcategories) query.WithCategoryTreePath(category.TreePath, null);
            else query.WithCategoryIds(null, category.Id);
        }
        if (filter.BrandId.HasValue)
        {
            if (!await StorefrontScope.Brands(db, request.Customer, request.StoreId).AnyAsync(x => x.Id == filter.BrandId.Value, ct))
                return db.Set<Product>().AsNoTracking().Where(x => false);
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
        return search.PrepareQuery(query, StorefrontScope.Products(db, request.Customer, request.StoreId,
            forSearch: !string.IsNullOrEmpty(term)).AsNoTracking());
    }

    // Conjunctive facets: same visible result set and filters as the list, independent of page.
    // Counts are direct product mappings, not a recursive sum of descendants.
    public async Task<CatalogFacetsDto> FacetsAsync(ProductSearchRequest filter, CancellationToken ct)
    {
        const int limit = 50;
        var products = (await SearchSourceAsync(filter, ct)).Select(x => x.Id).Distinct();
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
        return new(await products.CountAsync(ct), categories.Take(limit).ToArray(), brands.Take(limit).ToArray(),
            categories.Count > limit, brands.Count > limit);
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

        var summary = (await MapAsync(new[] { product }, true, false))[0];
        var imageIds = await db.Set<ProductMediaFile>().AsNoTracking()
            .Where(x => x.ProductId == id && !x.MediaFile.Deleted && !x.MediaFile.Hidden && !x.MediaFile.IsTransient)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).Select(x => x.MediaFileId).ToListAsync(ct);
        var images = new List<ProductImageDto>();
        foreach (var imageId in imageIds.Distinct())
            images.Add(new ProductImageDto(imageId, await media.GetUrlAsync(imageId, 800, doFallback: false)));

        var specifications = await db.Set<ProductSpecificationAttribute>().AsNoTracking()
            .Where(x => x.ProductId == id &&
                (x.ShowOnProductPage ?? x.SpecificationAttributeOption.SpecificationAttribute.ShowOnProductPage))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new ProductSpecificationDto(x.SpecificationAttributeOption.SpecificationAttributeId,
                x.SpecificationAttributeOption.SpecificationAttribute.Name, x.SpecificationAttributeOptionId,
                x.SpecificationAttributeOption.Name)).ToListAsync(ct);

        var quantities = product.ParseAllowedQuantities();
        var min = Math.Max(1, quantities.Length > 0 ? quantities[0] : product.OrderMinimumQuantity);
        var max = Math.Max(min, quantities.Length > 0 ? quantities[^1] : product.OrderMaximumQuantity);
        var supported = ProductSelection.UnsupportedCode(product) == null;
        var priceAllowed = await CanDisplayPriceAsync();
        var registered = request.Customer.IsRegistered();
        var canCart = registered && supported &&
            await permissions.AuthorizeAsync(StorefrontPermissions.Cart, request.Customer) &&
            await permissions.AuthorizeAsync(Permissions.Cart.AccessShoppingCart, request.Customer);
        var canWishlist = registered && supported && !product.DisableWishlistButton &&
            await permissions.AuthorizeAsync(StorefrontPermissions.Wishlist, request.Customer) &&
            await permissions.AuthorizeAsync(Permissions.Cart.AccessWishlist, request.Customer);

        return new ProductDetailDto
        {
            Id = summary.Id, Name = summary.Name, Sku = summary.Sku, ShortDescription = summary.ShortDescription,
            ImageUrl = summary.ImageUrl, MediaFileId = summary.MediaFileId, Price = summary.Price,
            FullDescriptionHtml = product.FullDescription, ProductTypeId = product.ProductTypeId,
            Images = images, Specifications = specifications,
            Variants = product.ProductVariantAttributes.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
                .Select(x => new ProductDetailVariantDto(x.Id, x.ProductAttribute.Name, x.AttributeControlTypeId,
                    x.IsRequired, x.TextPrompt, ProductSelection.SupportsControl(x.AttributeControlType),
                    x.ProductVariantAttributeValues.OrderBy(v => v.DisplayOrder).ThenBy(v => v.Id)
                        .Select(v => new ProductDetailVariantValueDto(v.Id, v.Name, v.IsPreSelected, v.Color)).ToList())).ToList(),
            PurchaseRules = new ProductPurchaseRulesDto(min, Math.Min(1000, max), Math.Max(1, product.QuantityStep),
                quantities.Where(q => q > 0 && q <= 1000).Distinct().ToArray(), product.HideQuantityControl,
                !supported || min > 1000),
            AllowedActions = new ProductCapabilitiesDto(supported && min <= 1000 && priceAllowed,
                canCart && min <= 1000, canWishlist && min <= 1000),
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
                selection.Warnings ?? [], product.RequireOtherProducts));

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

    public async Task<PriceDto> PriceAsync(int id, int quantity, CancellationToken ct)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product == null || product.CallForPrice || product.CustomerEntersPrice) return null;
        var options = prices.CreateDefaultOptions(false, request.Customer);
        return await Price(product, quantity, options);
    }

    public async Task<List<VariantDto>> VariantsAsync(int id, CancellationToken ct)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AsNoTracking().Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductAttribute)
            .Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductVariantAttributeValues)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product == null) return null;
        return product.ProductVariantAttributes.OrderBy(x => x.DisplayOrder)
            .Select(x => new VariantDto(x.Id, x.ProductAttribute.Name, x.AttributeControlTypeId, x.IsRequired,
                x.ProductVariantAttributeValues.OrderBy(v => v.DisplayOrder).Select(v => new VariantValueDto(v.Id, v.Name)).ToList())).ToList();
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
            var item = new ProductDto { Id = product.Id, Name = product.Name, ShortDescription = product.ShortDescription, Sku = product.Sku };
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
