using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Autofac;
using BonakMarket.Core;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Catalog;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Common;
using BonakMarket.Core.Catalog.Pricing;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Configuration;
using BonakMarket.StorefrontApi.Configuration;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class ProductCatalogMapper(SmartDbContext db, IPriceCalculationService prices,
    IMediaService media, IPermissionService permissions, IWorkContext work, IStoreContext stores,
    IComponentContext components, StorefrontRequestContext request, IConfiguration configuration, ISettingFactory settingFactory = null)
{
    internal Task<WholesaleCardSettings> LoadWholesaleSettingsAsync() => settingFactory == null
        ? Task.FromResult(new WholesaleCardSettings())
        : settingFactory.LoadSettingsAsync<WholesaleCardSettings>(0);

    public Task<bool> CanDisplayPriceAsync() => permissions.AuthorizeAsync(Permissions.Catalog.DisplayPrice, request.Customer);

    internal async Task<List<ProductDto>> MapAsync(IList<Product> products, bool includePrice, bool forListing = true, bool includeImages = true,
        int imageSize = 400, CancellationToken ct = default, bool includePurchaseInfo = true,
        WholesaleCardSettings wholesaleSettings = null)
    {
        if (products.Count == 0) return new List<ProductDto>();
        var priceAllowed = (includePrice || includePurchaseInfo) && await CanDisplayPriceAsync();
        var allowed = includePrice && priceAllowed;
        var options = allowed ? prices.CreateDefaultOptions(forListing, request.Customer, batchContext:
            new ProductBatchContext(products, db, components, stores.CurrentStore, request.Customer,
                includeHidden: false, loadMainMediaOnly: true)) : null;
        var ids = products.Select(x => x.Id).ToArray();
        wholesaleSettings ??= includePurchaseInfo || allowed ? await LoadWholesaleSettingsAsync() : new WholesaleCardSettings();
        var metadata = await LoadPackageMetadataAsync(ids, wholesaleSettings.UnitsPerPackageSpecificationId, includePurchaseInfo, ct);
        var languageId = work.WorkingLanguage.Id;
        var translated = await CatalogLocalization.LoadAsync(db, ids, languageId, nameof(Product),
            [nameof(Product.Name), nameof(Product.ShortDescription)], ct);
        string Localized(int id, string key, string fallback) => CatalogLocalization.ValueOrFallback(translated, id, key, fallback);
        var lookup = includeImages && ids.Length > 0
            ? await CatalogMedia.MainPictureIdsAsync(db, ids, ct) : new Dictionary<int, int>();
        var imageUrls = await CatalogMedia.UrlsAsync(media, lookup.Values.Select(x => (int?)x), imageSize, ct);
        var purchase = includePurchaseInfo ? await LoadPurchaseDataAsync(products, ids, languageId, ct) : null;
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
                var variant = purchase.VariantInfo.GetValueOrDefault(product.Id);
                var supported = ProductSelection.SupportsProduct(product) && !variant.UnsupportedRequired &&
                    quantities.Max >= quantities.Min;
                item.Availability = purchase.Sellable.Contains(product.Id) ? "available" : "out_of_stock";
                item.MinimumQuantity = quantities.Min;
                item.MaximumQuantity = quantities.Max;
                item.QuantityStep = Math.Max(1, product.QuantityStep);
                item.AllowedQuantities = quantities.Allowed;
                item.QuantityUnit = product.QuantityUnitId is int unitId && purchase.Units.TryGetValue(unitId, out var unit)
                    ? unit : purchase.DefaultUnit;
                item.UnitsPerPackage = WholesaleCardMetadata.PackageSize(metadata.GetValueOrDefault(product.Id));
                item.RequiresConfiguration = variant.HasVariants || !supported;
                var capacity = purchase.CombinationCapacity.GetValueOrDefault(product.Id);
                var minimumAvailable = product.ManageInventoryMethod switch
                {
                    ManageInventoryMethod.ManageStock => product.BackorderMode != BackorderMode.NoBackorders ||
                        product.StockQuantity >= quantities.Min,
                    ManageInventoryMethod.ManageStockByAttributes => capacity.Backorder || capacity.MaxStock >= quantities.Min,
                    _ => true
                };
                item.CanAddToCart = purchase.CanCart && supported && purchase.Sellable.Contains(product.Id) && minimumAvailable;
                item.HasDiscount = priceAllowed ? product.HasDiscountsApplied : null;
                item.IsPreOrder = product.AvailableForPreOrder;
            }
            if (allowed && !product.CallForPrice && !product.CustomerEntersPrice &&
                item.MinimumQuantity > 0 && item.MinimumQuantity <= item.MaximumQuantity)
                item.Price = await Price(product, item.MinimumQuantity, options);
            // A grouped or configurable product may be priced using another child/variant unit.
            // Do not infer the per-item price until that sale unit is unambiguous.
            if (!item.RequiresConfiguration && product.ProductType == ProductType.SimpleProduct && item.Price?.RetailPrice is decimal retailPrice &&
                WholesaleCardMetadata.ConsumerPricePerItem(retailPrice, item.UnitsPerPackage, item.QuantityUnit?.Id,
                    wholesaleSettings.PackageQuantityUnitId) is decimal consumerPrice)
            {
                item.ConsumerPrice = consumerPrice;
                item.ConsumerPriceCurrencyCode = item.Price.CurrencyCode;
            }
            items.Add(item);
        }
        return items;
    }

    internal static (int Min, int Max, int[] Allowed) QuantityRules(Product product)
    {
        var raw = product.ParseAllowedQuantities();
        var min = Math.Max(1, raw.Length > 0 ? raw[0] : product.OrderMinimumQuantity);
        var max = Math.Min(StorefrontLimits.MaxQuantity, Math.Max(min, raw.Length > 0 ? raw[^1] : product.OrderMaximumQuantity));
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
            result.RegularPrice?.Amount, result.DiscountAmount.Amount)
        {
            PricingQuantity = quantity,
            RetailPrice = result.RetailPrice?.Amount,
            TaxInclusive = options.TaxInclusive,
            SavingPercentage = result.Saving.HasSaving ? Math.Round((decimal)result.Saving.SavingPercent, 2) : null,
            SavingAmount = result.Saving.HasSaving ? result.Saving.SavingAmount?.Amount : null,
            SavingReference = result.Saving.HasSaving ? (result.RegularPrice.HasValue ? "regular_price" : "retail_price") : null
        };
    }

    // Batch-load product card data; never query per product.
    private async Task<Dictionary<int, decimal>> LoadPackageMetadataAsync(int[] ids, int packageAttributeId,
        bool includePurchaseInfo, CancellationToken ct)
    {
        var metadata = new Dictionary<int, decimal>();
        if (includePurchaseInfo && packageAttributeId > 0)
        {
            var rows = await db.Set<ProductSpecificationAttribute>().AsNoTracking()
                .Where(x => ids.Contains(x.ProductId) && x.SpecificationAttributeOption.SpecificationAttributeId == packageAttributeId)
                .Select(x => new { x.ProductId, Value = x.SpecificationAttributeOption.NumberValue }).ToListAsync(ct);
            metadata = rows.GroupBy(x => x.ProductId).Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single().Value);
        }
        return metadata;
    }

    private async Task<ProductPurchaseData> LoadPurchaseDataAsync(IList<Product> products, int[] ids, int languageId, CancellationToken ct)
    {
        var sellable = (await ProductCatalogQueries.ApplyStockFilter(db.Set<Product>().AsNoTracking().Where(x => ids.Contains(x.Id)), true)
            .Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var managedIds = products.Where(x => x.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes)
            .Select(x => x.Id).ToArray();
        var combinationCapacity = new Dictionary<int, (int MaxStock, bool Backorder)>();
        if (managedIds.Length > 0)
        {
            var combinations = await db.Set<ProductVariantAttributeCombination>().AsNoTracking()
                .Where(x => managedIds.Contains(x.ProductId) && x.IsActive)
                .Select(x => new { x.ProductId, x.StockQuantity, x.AllowOutOfStockOrders }).ToListAsync(ct);
            combinationCapacity = combinations.GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => (g.Max(x => x.StockQuantity), g.Any(x => x.AllowOutOfStockOrders)));
        }
        var variants = await db.Set<ProductVariantAttribute>().AsNoTracking().Where(x => ids.Contains(x.ProductId))
            .Select(x => new { x.ProductId, x.IsRequired, x.AttributeControlTypeId }).ToListAsync(ct);
        var variantInfo = variants.GroupBy(x => x.ProductId).ToDictionary(g => g.Key,
            g => (true, g.Any(x => x.IsRequired && !ProductSelection.SupportsControl((AttributeControlType)x.AttributeControlTypeId))));
        var (unitLookup, defaultUnit) = await LoadUnitsAsync(products, languageId, ct);
        var canCart = (request.Customer.IsRegistered()
            ? await permissions.AuthorizeAsync(StorefrontPermissions.Cart, request.Customer)
            : GuestCartPolicy.Enabled(configuration) && GuestCartPolicy.IsGuest(request.Customer) &&
                await permissions.AuthorizeAsync(StorefrontPermissions.GuestCart, request.Customer)) &&
            await permissions.AuthorizeAsync(Permissions.Cart.AccessShoppingCart, request.Customer);
        return new(sellable, combinationCapacity, variantInfo, unitLookup, defaultUnit, canCart);
    }

    private async Task<(Dictionary<int, QuantityUnitDto> Units, QuantityUnitDto Default)> LoadUnitsAsync(
        IList<Product> products, int languageId, CancellationToken ct)
    {
        var unitIds = products.Select(x => x.QuantityUnitId).Where(x => x > 0).Select(x => x.Value).Distinct().ToArray();
        var units = await db.Set<QuantityUnit>().AsNoTracking().Where(x => unitIds.Contains(x.Id) || x.IsDefault)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync(ct);
        var allUnitIds = units.Select(x => x.Id).ToArray();
        var labels = await CatalogLocalization.LoadAsync(db, allUnitIds, languageId, nameof(QuantityUnit),
            [nameof(QuantityUnit.Name), nameof(QuantityUnit.NamePlural)], ct);
        string UnitLabel(int id, string key, string fallback) => CatalogLocalization.ValueOrFallback(labels, id, key, fallback);
        var unitLookup = units.ToDictionary(x => x.Id, x => new QuantityUnitDto(x.Id,
            UnitLabel(x.Id, nameof(QuantityUnit.Name), x.Name), UnitLabel(x.Id, nameof(QuantityUnit.NamePlural), x.NamePlural)));
        var defaultEntity = units.FirstOrDefault(x => x.IsDefault);
        return (unitLookup, defaultEntity != null ? unitLookup[defaultEntity.Id] : null);
    }
}
