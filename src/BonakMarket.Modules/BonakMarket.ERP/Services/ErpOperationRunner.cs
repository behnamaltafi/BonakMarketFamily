using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Infrastructure;

namespace BonakMarket.ERP.Services;

public sealed class ErpOperationRunner(SmartDbContext db, ErpOperationRouter router, ErpCatalogService catalog, ErpOperationLock locks)
{
    public async Task<int> StageProductsAsync(string company, int expectedConnectionId, int skip, int take, CancellationToken ct)
    {
        var initial = await router.ResolveAsync(company, "Products", ct);
        if (initial.Connection.Id != expectedConnectionId) throw new ErpIntegrationException("operation_connection_mismatch");
        await using var lease = await locks.AcquireAsync(ErpOperationLock.ProductsKey(initial.Route.CompanyCode), ct);
        var current = await router.ResolveAsync(company, "Products", ct);
        if (current.Connection.Id != expectedConnectionId) throw new ErpIntegrationException("operation_connection_mismatch");
        return await catalog.StageProductsAsync(current.Connection, skip, take, ct);
    }

    public async Task RunScheduledPageAsync(int routeId, CancellationToken ct)
    {
        var company = await db.Set<ErpOperationConfiguration>().AsNoTracking().Where(x => x.Id == routeId)
            .Select(x => x.CompanyCode).SingleOrDefaultAsync(ct);
        if (company == null) return;
        await using var lease = await locks.AcquireAsync(ErpOperationLock.ProductsKey(company), ct);
        var route = await db.Set<ErpOperationConfiguration>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == routeId, ct);
        if (route == null || !route.Enabled || !route.ScheduleEnabled || route.Operation != "Products" ||
            (route.NextRunUtc.HasValue && route.NextRunUtc.Value > DateTime.UtcNow)) return;
        try
        {
            var current = await router.ResolveAsync(route.CompanyCode, route.Operation, ct);
            if (current.Route.Revision != route.Revision) return;
            var count = await catalog.StageProductsAsync(current.Connection, route.NextOffset, route.BatchSize, ct);
            var completed = count < route.BatchSize;
            var now = DateTime.UtcNow;
            var offset = completed ? 0 : checked(route.NextOffset + count);
            var due = completed ? now.AddMinutes(route.IntervalMinutes) : now;
            await db.Set<ErpOperationConfiguration>().Where(x => x.Id == route.Id && x.Revision == route.Revision)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextOffset, offset)
                    .SetProperty(x => x.NextRunUtc, (DateTime?)due)
                    .SetProperty(x => x.LastSucceededUtc, completed ? (DateTime?)now : route.LastSucceededUtc), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            // Keep the checkpoint; retry a failed page after five minutes.
            var due = DateTime.UtcNow.AddMinutes(5);
            await db.Set<ErpOperationConfiguration>().Where(x => x.Id == route.Id && x.Revision == route.Revision)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextRunUtc, (DateTime?)due), ct);
            throw;
        }
    }
}
