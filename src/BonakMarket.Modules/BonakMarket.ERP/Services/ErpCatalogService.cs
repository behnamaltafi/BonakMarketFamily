using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.ERP.Contracts;
using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Services;

public sealed class ErpCatalogService(SmartDbContext db, ErpProviderResolver resolver)
{
    public async Task<object> PreviewAsync(ErpConnection connection, string operation, int skip, int take, CancellationToken ct)
        => operation switch
        {
            "Products" => (await resolver.Resolve<IErpProductProvider>(connection.Provider).GetProductsAsync(connection, skip, take, ct)).Items,
            "Prices" => (await resolver.Resolve<IErpPriceProvider>(connection.Provider).GetPricesAsync(connection, skip, take, ct)).Items,
            "Inventory" => (await resolver.Resolve<IErpInventoryProvider>(connection.Provider).GetInventoryAsync(connection, skip, take, ct)).Items,
            "Categories" => (await resolver.Resolve<IErpClassificationProvider>(connection.Provider).GetCategoriesAsync(connection, skip, take, ct)).Items,
            "Warehouses" => (await resolver.Resolve<IErpClassificationProvider>(connection.Provider).GetWarehousesAsync(connection, skip, take, ct)).Items,
            _ => throw new ErpIntegrationException("unsupported_operation")
        };

    // A bounded, repeatable import, with an explicit staging boundary. No live price,
    // inventory, publication state or existing product content is modified.
    public async Task<int> StageProductsAsync(ErpConnection c, int skip, int take, CancellationToken ct)
    {
        // The caller holds the operation lock; any older Running record for
        // this connection and operation cannot still own that lock.
        var now = DateTime.UtcNow;
        await db.Set<ErpSyncRun>().Where(x => x.ConnectionId == c.Id && x.Operation == "Products" && x.Status == "Running")
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, "Interrupted")
                .SetProperty(x => x.ErrorCode, "previous_execution_interrupted")
                .SetProperty(x => x.FinishedUtc, (DateTime?)now), ct);
        var run = new ErpSyncRun { ConnectionId = c.Id, Operation = "Products", Status = "Running",
            StartedUtc = now, Offset = skip, CorrelationId = Guid.NewGuid() };
        db.Set<ErpSyncRun>().Add(run);
        await db.SaveChangesAsync(ct);
        try
        {
            var page = await resolver.Resolve<IErpProductProvider>(c.Provider).GetProductsAsync(c, skip, take, ct);
            var ids = page.Items.Select(x => x.ExternalId).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ErpIntegrationException("duplicate_external_id");
            using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var existing = await db.Set<ErpCatalogProduct>().Where(x => x.CompanyCode == c.CompanyCode && ids.Contains(x.ExternalId))
                .ToDictionaryAsync(x => x.ExternalId, ct);
            foreach (var p in page.Items)
            {
                if (!existing.TryGetValue(p.ExternalId, out var row))
                {
                    row = new ErpCatalogProduct { CompanyCode = c.CompanyCode, ExternalId = p.ExternalId, ConnectionId = c.Id };
                    db.Set<ErpCatalogProduct>().Add(row);
                }
                // Never take over a mapping from a different connection implicitly.
                if (row.ConnectionId != c.Id) throw new ErpIntegrationException("external_id_connection_conflict");
                row.Sku = p.Sku;
                row.Name = p.Name;
                row.ActiveForSale = p.ActiveForSale;
                row.SnapshotJson = JsonSerializer.Serialize(p);
                row.LastSeenUtc = DateTime.UtcNow;
            }
            run.Status = "Succeeded";
            run.Rows = ids.Length;
            run.FinishedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ids.Length;
        }
        catch (Exception ex)
        {
            // A failed or partial page cannot modify staging data. Record only a safe error code.
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is ErpCatalogProduct || x.Entity is ErpSyncRun).ToArray())
                entry.State = EntityState.Detached;
            var status = ex is OperationCanceledException && ct.IsCancellationRequested ? "Cancelled" : "Failed";
            var code = ex is ErpIntegrationException erp ? erp.Code : status == "Cancelled" ? "cancelled" : "catalog_sync_failed";
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await db.Set<ErpSyncRun>().Where(x => x.Id == run.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, status)
                        .SetProperty(x => x.ErrorCode, code)
                        .SetProperty(x => x.FinishedUtc, (DateTime?)DateTime.UtcNow), cleanup.Token);
            }
            catch { /* Preserve original failure; next run marks stale state interrupted. */ }
            throw;
        }
    }
}
