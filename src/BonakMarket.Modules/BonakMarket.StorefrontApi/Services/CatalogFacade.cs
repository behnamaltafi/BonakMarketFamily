using Microsoft.EntityFrameworkCore;
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
using BonakMarket.Core.Identity;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class CatalogFacade(SmartDbContext db, IPriceCalculationService prices,
    IMediaService media, IPermissionService permissions, StorefrontRequestContext request,
    IProductAttributeMaterializer attributes, IShoppingCartValidator validator,
    ProductCatalogQueries queries, ProductCatalogMapper mapper)
{

    public Task<bool> CanDisplayPriceAsync() => mapper.CanDisplayPriceAsync();

    public async Task<PageDto<ProductDto>> SearchAsync(ProductSearchRequest filter, CancellationToken ct, SearchSettings settings = null)
    {
        var source = await queries.SearchSourceAsync(filter, ct, settings);
        if (filter.Sort == CatalogSort.BestSellers)
        {
            var orders = CatalogPurchaseRanking.EligibleOrders(db.Set<Order>().AsNoTracking(), request.StoreId,
                DateTime.UtcNow.AddDays(-90));
            source = CatalogPurchaseRanking.BestSellers(source, CatalogPurchaseRanking.Items(db.Set<OrderItem>().AsNoTracking(), orders));
        }
        return await source.PageAsync(filter, items => mapper.MapAsync(items, filter.IncludePrice, ct: ct), ct);
    }

    public async Task<PageDto<ProductDto>> FrequentlyPurchasedAsync(ProductFilterRequest filter, CancellationToken ct, SearchSettings settings = null)
    {
        var orders = CatalogPurchaseRanking.EligibleOrders(StorefrontScope.Orders(db, request.Customer, request.StoreId)
            .AsNoTracking(), request.StoreId, DateTime.UtcNow.AddDays(-365));
        var products = await queries.SearchSourceAsync(filter, ct, settings);
        var source = CatalogPurchaseRanking.FrequentlyPurchased(products,
            CatalogPurchaseRanking.Items(db.Set<OrderItem>().AsNoTracking(), orders));
        return await source.PageAsync(filter, items => mapper.MapAsync(items, filter.IncludePrice, ct: ct), ct);
    }

    internal static (bool Enabled, int MinimumTermLength, int Limit) InstantSearchPolicy(
        InstantSearchRequest filter, SearchSettings settings)
    {
        var limit = Math.Min(filter.Limit ?? StorefrontLimits.MaxSuggestions, Math.Clamp(settings.InstantSearchNumberOfProducts, 0, StorefrontLimits.MaxSuggestions));
        return (settings.InstantSearchEnabled && limit > 0, Math.Clamp(settings.InstantSearchTermMinLength, 1, StorefrontLimits.MaxSearchLength), limit);
    }

    public async Task<InstantSearchDto> InstantSearchAsync(InstantSearchRequest filter, SearchSettings settings, CancellationToken ct)
    {
        var term = filter.Search?.Trim() ?? string.Empty;
        var (enabled, minimum, limit) = InstantSearchPolicy(filter, settings);
        if (!enabled || term.Length < minimum)
            return new(term, enabled, minimum, limit, false, Array.Empty<InstantSearchProductDto>());

        // Use exactly the same term, search settings, visibility and availability as the full results.
        var source = await queries.SearchSourceAsync(new ProductSearchRequest { Search = term }, ct, settings);
        var products = await source.Take(limit + 1).ToListAsync(ct);
        var hasMore = products.Count > limit;
        if (hasMore) products.RemoveAt(limit);

        if (products.Count == 0)
            return new(term, enabled, minimum, limit, false, Array.Empty<InstantSearchProductDto>());

        var mapped = await mapper.MapAsync(products, false, includeImages: settings.ShowProductImagesInInstantSearch,
            imageSize: 120, ct: ct, includePurchaseInfo: false);
        var items = mapped.Select(x => new InstantSearchProductDto(x.Id, x.Name,
            x.Sku, x.MediaFileId, x.ImageUrl)).ToArray();
        return new(term, enabled, minimum, limit, hasMore, items);
    }

    public Task<CatalogFacetsDto> FacetsAsync(ProductFacetsRequest filter, CancellationToken ct,
        SearchSettings settings = null, string priceCurrencyCode = null) =>
        queries.FacetsAsync(filter, ct, settings, priceCurrencyCode);

    public async Task<ProductDto> FindAsync(int id, CancellationToken ct)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product == null) return null;
        return (await mapper.MapAsync(new[] { product }, true, false, ct: ct))[0];
    }

    public async Task<ProductDetailDto> DetailAsync(int id, CancellationToken ct)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AsNoTracking().Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductAttribute)
            .Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductVariantAttributeValues)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product == null) return null;

        var wholesaleSettings = await mapper.LoadWholesaleSettingsAsync();
        var summary = (await mapper.MapAsync(new[] { product }, true, false, ct: ct, wholesaleSettings: wholesaleSettings))[0];
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
            UnitsPerPackage = summary.UnitsPerPackage, ConsumerPrice = summary.ConsumerPrice,
            ConsumerPriceCurrencyCode = summary.ConsumerPriceCurrencyCode,
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

}
