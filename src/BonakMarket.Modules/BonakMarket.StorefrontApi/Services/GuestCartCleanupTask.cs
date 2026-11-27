using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Engine.Initialization;
using BonakMarket.Events;
using BonakMarket.Scheduling;
using BonakMarket.StorefrontApi.Domain;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class GuestCartCleanupTask(SmartDbContext db, GuestCartSqlLock locks, ILogger<GuestCartCleanupTask> logger) : ITask
{
    public async Task Run(TaskExecutionContext ctx, CancellationToken cancelToken = default)
    {
        if (!locks.Supported) return;
        var now = DateTime.UtcNow;
        var hashes = await db.Set<GuestCartToken>().AsNoTracking().Where(t => t.ExpiresOnUtc <= now)
            .OrderBy(t => t.ExpiresOnUtc).ThenBy(t => t.Id).Select(t => t.TokenHash).Take(100).ToListAsync(cancelToken);
        foreach (var hash in hashes)
        {
            try
            {
                await using var lease = await locks.AcquireAsync(GuestCartSqlLock.GuestKey(hash), cancelToken);
                await using var tx = await db.Database.BeginTransactionAsync(cancelToken);
                var token = await db.Set<GuestCartToken>().FirstOrDefaultAsync(t => t.TokenHash == hash && t.ExpiresOnUtc <= now, cancelToken);
                if (token == null) continue;
                var customer = await db.Customers.IgnoreQueryFilters().IncludeCustomerRoles().FirstOrDefaultAsync(c => c.Id == token.CustomerId, cancelToken);
                if (GuestCartPolicy.IsDedicatedGuestAccount(customer))
                    await db.ShoppingCartItems.Where(x => x.CustomerId == token.CustomerId && x.StoreId == token.StoreId &&
                        x.ShoppingCartTypeId == (int)ShoppingCartType.ShoppingCart).ExecuteDeleteAsync(cancelToken);
                db.Set<GuestCartToken>().Remove(token);
                await db.SaveChangesAsync(cancelToken);
                await tx.CommitAsync(cancelToken);
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning("Guest cart cleanup deferred: {ErrorType}", ex.GetType().Name);
            }
        }
        // Native DeleteGuestsTask subsequently removes empty guest Customers and generic attributes.
    }
}
