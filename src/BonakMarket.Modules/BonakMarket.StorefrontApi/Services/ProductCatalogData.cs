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

// Batch-load the data required by product cards, without per-product EF queries.
public sealed class ProductCatalogData(SmartDbContext db, StorefrontRequestContext request,
    IPermissionService permissions, IConfiguration configuration)
{
    internal async Task<Dictionary<int, decimal>> LoadPackageMetadataAsync(int[] ids, int packageAttributeId,
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

    internal async Task<PurchaseData> LoadPurchaseDataAsync(IList<Product> products, int[] ids, int languageId, CancellationToken ct)
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

    internal sealed record PurchaseData(HashSet<int> Sellable,
        Dictionary<int, (int MaxStock, bool Backorder)> CombinationCapacity,
        Dictionary<int, (bool HasVariants, bool UnsupportedRequired)> VariantInfo,
        Dictionary<int, QuantityUnitDto> Units, QuantityUnitDto DefaultUnit, bool CanCart);
}
