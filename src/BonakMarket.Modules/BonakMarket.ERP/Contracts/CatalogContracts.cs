using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Contracts;

public interface IErpProvider { string SystemName { get; } }

public sealed record ErpPage<T>(IReadOnlyList<T> Items);
public sealed record ErpProduct(string ExternalId, string Sku, string Name,
    bool ActiveForSale, bool ActiveForStock, string BrandId, string BrandName,
    string ManufacturerId, string ManufacturerName, string CategoryId,
    string UnitName, string PackUnitName, decimal? UnitsPerPack,
    string Barcode, string Gtin, decimal? PublicSalePrice, decimal? ConsumerPrice,
    decimal? ManufacturerPrice, decimal? Tax, decimal? Charge, string SourceModifiedDate = null);
public sealed record ErpPrice(string ExternalId, string ProductExternalId,
    decimal? SalePrice, decimal? ConsumerPrice, decimal? ManufacturerPrice,
    string StartDate, string EndDate, string DistributionId, string CustomerId,
    string CustomerCategoryId, string CustomerActivityId, string CustomerLevelId,
    string PaymentTypeId, string PaymentUsanceId, string OrderTypeId,
    string StateId, string CountyId, string AreaId, string MainCustomerTypeId,
    string SubCustomerTypeId, int? RateCode);
public sealed record ErpStock(string ExternalId, string ProductExternalId,
    string WarehouseId, decimal AvailableQuantity, decimal? UnitsPerPack,
    bool IsBatch, decimal? PublicSalePrice, bool IsAggregate = false);
public sealed record ErpCategory(string ExternalId, string ParentExternalId, string Name, string Path);
public sealed record ErpWarehouse(string ExternalId, string Code, string Name, string DistributionId);

public interface IErpProductProvider : IErpProvider
{
    Task<ErpPage<ErpProduct>> GetProductsAsync(ErpConnection connection, int skip, int take, CancellationToken ct);
}
public interface IErpPriceProvider : IErpProvider
{
    Task<ErpPage<ErpPrice>> GetPricesAsync(ErpConnection connection, int skip, int take, CancellationToken ct);
}
public interface IErpInventoryProvider : IErpProvider
{
    Task<ErpPage<ErpStock>> GetInventoryAsync(ErpConnection connection, int skip, int take, CancellationToken ct);
}
public interface IErpCategoryProvider : IErpProvider
{
    Task<ErpPage<ErpCategory>> GetCategoriesAsync(ErpConnection connection, int skip, int take, CancellationToken ct);
}
public interface IErpWarehouseProvider : IErpProvider
{
    Task<ErpPage<ErpWarehouse>> GetWarehousesAsync(ErpConnection connection, int skip, int take, CancellationToken ct);
}
