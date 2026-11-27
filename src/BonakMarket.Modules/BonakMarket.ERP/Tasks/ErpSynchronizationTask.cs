using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Data;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Services;
using BonakMarket.Scheduling;

namespace BonakMarket.ERP.Tasks;

// One bounded page per due company each tick. No automatic price/stock writes.
public sealed class ErpSynchronizationTask(SmartDbContext db, ErpOperationRunner runner, ILogger<ErpSynchronizationTask> logger) : ITask
{
    public async Task Run(TaskExecutionContext ctx, CancellationToken cancelToken = default)
    {
        var now = DateTime.UtcNow;
        var ids = await db.Set<ErpOperationConfiguration>().AsNoTracking()
            .Where(x => x.Enabled && x.ScheduleEnabled && x.Operation == "Products" && (x.NextRunUtc == null || x.NextRunUtc <= now))
            .OrderBy(x => x.NextRunUtc).ThenBy(x => x.Id).Select(x => x.Id).Take(10).ToArrayAsync(cancelToken);
        foreach (var id in ids)
        {
            cancelToken.ThrowIfCancellationRequested();
            try { await runner.RunScheduledPageAsync(id, cancelToken); }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested) { throw; }
            catch (ErpIntegrationException ex) when (ex.Code == "operation_already_running") { }
            catch (Exception ex)
            {
                logger.LogWarning("ERP operation {OperationId} failed: {Code}", id,
                    ex is ErpIntegrationException erp ? erp.Code : "sync_failed");
            }
        }
    }

    public static async Task EnsureRegisteredAsync(ITaskStore store)
    {
        var task = await store.GetOrAddTaskAsync<ErpSynchronizationTask>(x =>
        {
            x.Name = "ERP catalog synchronization";
            x.CronExpression = "* * * * *";
            x.Enabled = true;
            x.StopOnError = false;
            x.RunPerMachine = false;
        });
        if (task.Enabled && !task.NextRunUtc.HasValue)
            await store.CalculateFutureSchedulesAsync(new[] { task });
    }
}
