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
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

internal sealed class ProductCatalogMapper(SmartDbContext db, IPriceCalculationService prices,
    IMediaService media, IPermissionService permissions, IWorkContext work, IStoreContext stores,
    IComponentContext components, StorefrontRequestContext request, IConfiguration configuration, ISettingFactory settingFactory)
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
        var packageAttributeId = wholesaleSettings.UnitsPerPackageSpecificationId;
        var metadata = new Dictionary<int, decimal>();
        if (includePurchaseInfo && packageAttributeId > 0)
        {
            var rows = await db.Set<ProductSpecificationAttribute>().AsNoTracking()
                .Where(x => ids.Contains(x.ProductId) && x.SpecificationAttributeOption.SpecificationAttributeId == packageAttributeId)
                .Select(x => new { x.ProductId, Value = x.SpecificationAttributeOption.NumberValue }).ToListAsync(ct);
            metadata = rows.GroupBy(x => x.ProductId).Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single().Value);
        }
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
        var combinationCapacity = new Dictionary<int, (int MaxStock, bool Backorder)>();
        var variantInfo = new Dictionary<int, (bool HasVariants, bool UnsupportedRequired)>();
        var unitLookup = new Dictionary<int, QuantityUnitDto>();
        QuantityUnitDto defaultUnit = null;
        var canCart = false;
        if (includePurchaseInfo)
        {
            sellable = (await ProductCatalogQueries.ApplyStockFilter(db.Set<Product>().AsNoTracking().Where(x => ids.Contains(x.Id)), true)
                .Select(x => x.Id).ToListAsync(ct)).ToHashSet();
            var managedIds = products.Where(x => x.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes)
                .Select(x => x.Id).ToArray();
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
            canCart = (request.Customer.IsRegistered()
                ? await permissions.AuthorizeAsync(StorefrontPermissions.Cart, request.Customer)
                : GuestCartPolicy.Enabled(configuration) && GuestCartPolicy.IsGuest(request.Customer) &&
                    await permissions.AuthorizeAsync(StorefrontPermissions.GuestCart, request.Customer)) &&
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
                item.UnitsPerPackage = WholesaleCardMetadata.PackageSize(metadata.GetValueOrDefault(product.Id));
                item.RequiresConfiguration = variant.HasVariants || !supported;
                var capacity = combinationCapacity.GetValueOrDefault(product.Id);
                var minimumAvailable = product.ManageInventoryMethod switch
                {
                    ManageInventoryMethod.ManageStock => product.BackorderMode != BackorderMode.NoBackorders ||
                        product.StockQuantity >= quantities.Min,
                    ManageInventoryMethod.ManageStockByAttributes => capacity.Backorder || capacity.MaxStock >= quantities.Min,
                    _ => true
                };
                item.CanAddToCart = canCart && supported && sellable.Contains(product.Id) && minimumAvailable;
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
}
