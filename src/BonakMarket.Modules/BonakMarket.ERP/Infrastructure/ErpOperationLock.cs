using System.Data;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.ERP.Services;

namespace BonakMarket.ERP.Infrastructure;

// SQL Server session-scoped application lock: no long-running database
// transaction during ERP HTTP calls. Released on disposal or session loss.
public sealed class ErpOperationLock(SmartDbContext db)
{
    public static string ProductsKey(string companyCode) => "BonakMarket:ERP:Products:" + companyCode.Trim().ToUpperInvariant();

    public async Task<IAsyncDisposable> AcquireAsync(string resource, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "DECLARE @result int; EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @result;";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@resource"; parameter.Value = resource; command.Parameters.Add(parameter);
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            if (result < 0) throw new ErpIntegrationException("operation_already_running");
            return new Lease(db, resource);
        }
        catch { await db.Database.CloseConnectionAsync(); throw; }
    }

    private sealed class Lease(SmartDbContext db, string resource) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
                command.CommandTimeout = 5;
                var parameter = command.CreateParameter(); parameter.ParameterName = "@resource"; parameter.Value = resource; command.Parameters.Add(parameter);
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
