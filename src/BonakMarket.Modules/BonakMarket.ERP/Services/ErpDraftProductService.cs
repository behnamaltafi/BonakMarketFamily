using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Data;
using BonakMarket.Core.Seo;
using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Services;

public sealed record DraftProductResult(int ProductId, int ConnectionId);

/// <summary>Explicit draft creation, not synchronization of live saleable fields.</summary>
public sealed class ErpDraftProductService(SmartDbContext db, IUrlService urls)
{
    public async Task<DraftProductResult> CreateAsync(int id, int templateId, CancellationToken ct)
    {
        using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var row = await db.Set<ErpCatalogProduct>().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ErpIntegrationException("staged_product_not_found");
        if (row.ProductId is int existing)
            return new DraftProductResult(existing, row.ConnectionId);
        if (!row.ActiveForSale) throw new ErpIntegrationException("erp_product_inactive");
        if (!await db.Set<ProductTemplate>().AnyAsync(x => x.Id == templateId, ct))
            throw new ErpIntegrationException("invalid_product_template");
        if (await db.Products.IgnoreQueryFilters().AnyAsync(x => x.Sku == row.Sku, ct) ||
            await db.Set<ProductVariantAttributeCombination>().IgnoreQueryFilters().AnyAsync(x => x.Sku == row.Sku, ct))
            throw new ErpIntegrationException("sku_exists_link_existing_product");
        var now = DateTime.UtcNow;
        var product = new Product
        {
            Name = row.Name,
            Sku = row.Sku,
            ProductType = ProductType.SimpleProduct,
            ProductTemplateId = templateId,
            Visibility = ProductVisibility.Full,
            Condition = ProductCondition.New,
            Published = false,
            DisableBuyButton = true,
            DisableWishlistButton = true,
            ManageInventoryMethod = ManageInventoryMethod.ManageStock,
            StockQuantity = 0,
            BackorderMode = BackorderMode.NoBackorders,
            Price = 0,
            IsShippingEnabled = true,
            OrderMinimumQuantity = 1,
            OrderMaximumQuantity = 50,
            QuantityStep = 1,
            CreatedOnUtc = now,
            UpdatedOnUtc = now
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        var slug = await urls.ValidateSlugAsync(product, null, product.Name, true, force: true);
        await urls.ApplySlugAsync(slug);
        row.ProductId = product.Id;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new DraftProductResult(product.Id, row.ConnectionId);
    }
}
