using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.ERP.Contracts;
using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Services;

public static class ErpOperations
{
    public static readonly string[] ReadOperations = ["Products", "Prices", "Inventory", "Categories", "Warehouses"];
    public static Type Capability(string operation) => operation switch
    {
        "Products" => typeof(IErpProductProvider),
        "Prices" => typeof(IErpPriceProvider),
        "Inventory" => typeof(IErpInventoryProvider),
        "Categories" or "Warehouses" => typeof(IErpClassificationProvider),
        _ => throw new ErpIntegrationException("unsupported_operation")
    };
}
public sealed class ErpOperationRouter(SmartDbContext db, ErpProviderResolver providers)
{
    public void Validate(ErpOperationConfiguration route, ErpConnection connection)
    {
        if (!string.Equals(route.CompanyCode, connection.CompanyCode, StringComparison.OrdinalIgnoreCase))
            throw new ErpIntegrationException("connection_company_mismatch");
        if (!providers.Supports(connection.Provider, ErpOperations.Capability(route.Operation)))
            throw new ErpIntegrationException("unsupported_provider_operation");
        if (route.ScheduleEnabled && route.Operation != "Products")
            throw new ErpIntegrationException("operation_schedule_not_supported");
        if (route.BatchSize is < 1 or > 100 || route.IntervalMinutes is < 1 or > 10080)
            throw new ErpIntegrationException("invalid_operation_schedule");
    }

    public async Task<(ErpOperationConfiguration Route, ErpConnection Connection)> ResolveAsync(
        string company, string operation, CancellationToken ct)
    {
        var route = await db.Set<ErpOperationConfiguration>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyCode == company && x.Operation == operation, ct);
        if (route == null || !route.Enabled) throw new ErpIntegrationException("operation_not_configured");
        var connection = await db.Set<ErpConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == route.ConnectionId, ct);
        if (connection == null || !connection.Enabled) throw new ErpIntegrationException("connection_disabled");
        Validate(route, connection);
        return (route, connection);
    }
}
