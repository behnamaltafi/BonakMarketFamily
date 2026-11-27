using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;

namespace BonakMarket.StorefrontApi.Security;

// SQL Server application locks coordinate API requests across replicas without Redis.
public sealed class GuestCartSqlLock(SmartDbContext db)
{
    public static string GuestKey(string hash) => "storefront:guest-cart:" + hash;
    public static string CustomerKey(int id) => "storefront:customer-cart:" + id;
    public bool Supported => db.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;

    public async Task<IAsyncDisposable> AcquireAsync(string resource, CancellationToken ct)
    {
        if (!Supported) throw new InvalidOperationException("Guest cart requires SQL Server.");
        await db.Database.OpenConnectionAsync(ct);
        var connection = db.Database.GetDbConnection();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=10000; SELECT @result;";
            AddParameter(cmd, resource);
            var result = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            if (result < 0) throw new TimeoutException("Cart lock unavailable.");
            return new GuestCartSqlLockLease(db, connection, resource);
        }
        catch
        {
            await db.Database.CloseConnectionAsync();
            throw;
        }
    }
    internal static void AddParameter(DbCommand command, string resource)
    {
        var p = command.CreateParameter();
        p.ParameterName = "@resource";
        p.Value = resource;
        command.Parameters.Add(p);
    }
}
