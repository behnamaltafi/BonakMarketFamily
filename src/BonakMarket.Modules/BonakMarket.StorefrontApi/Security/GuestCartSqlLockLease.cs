using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;

namespace BonakMarket.StorefrontApi.Security;

internal sealed class GuestCartSqlLockLease(SmartDbContext db, DbConnection connection, string resource) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "EXEC sys.sp_releaseapplock @Resource=@resource, @LockOwner='Session';";
            GuestCartSqlLock.AddParameter(cmd, resource);
            await cmd.ExecuteNonQueryAsync(CancellationToken.None);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
