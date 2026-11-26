using System.Globalization;
using BonakMarket.ERP.Infrastructure;
using BonakMarket.ERP.Contracts;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Services;

namespace BonakMarket.ERP.Providers.Varanegar;

public sealed class VaranegarProvider(VaranegarODataClient odata, ErpEndpointPolicy endpoints) :
    IErpProductProvider, IErpPriceProvider, IErpInventoryProvider, IErpCategoryProvider, IErpWarehouseProvider, IErpProviderConfiguration
{
    public string SystemName => "Varanegar";
    public string DefaultSettingsJson => ErpSettingsJson.Write(new VaranegarSettings());
    public void Validate(ErpConnection c)
    {
        var s = ErpSettingsJson.Read<VaranegarSettings>(c.ProviderSettingsJson);
        var root = endpoints.ValidateBase(c.BaseUrl);
        var prefix = s.ODataPrefix?.Trim('/');
        foreach (var path in new[] { s.ProductsPath, s.PricesPath, s.InventoryPath, s.CategoriesPath, s.WarehousesPath })
        {
            endpoints.Relative(root, path);
            endpoints.Relative(root, string.IsNullOrEmpty(prefix) ? path : prefix + "/" + path.TrimStart('/'));
        }
    }

    public async Task<ErpPage<ErpProduct>> GetProductsAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await odata.ReadAsync<ProductRow>(c, ErpSettingsJson.Read<VaranegarSettings>(c.ProviderSettingsJson).ProductsPath, skip, take, ct))
            .Select(x => new ErpProduct(Id(x.Id), Required(x.GoodsCode), Required(x.GoodsName),
                Flag(x.NoSaleFlag), Flag(x.NoStockFlag), OptionalId(x.BrandRef), x.BrandName,
                OptionalId(x.ManufacturerRef), x.ManufacturerName, OptionalId(x.GoodsGroupRef),
                x.UnitName, x.PackUnitName, x.CartonType, x.DefaultBarcode ?? x.Barcode1, x.GTIN,
                ValidPrice(x.SalePrice), ValidPrice(x.UserPrice), ValidPrice(x.ManufacturerPrice), x.Tax, x.Charge, x.ModifiedDate)).ToArray());

    public async Task<ErpPage<ErpPrice>> GetPricesAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await odata.ReadAsync<PriceRow>(c, ErpSettingsJson.Read<VaranegarSettings>(c.ProviderSettingsJson).PricesPath, skip, take, ct))
            .Select(x => new ErpPrice(Id(x.Id), Id(x.GoodsRef), ValidPrice(x.SalePrice),
                ValidPrice(x.UserPrice), ValidPrice(x.ManufacturerPrice), x.StartDate, x.EndDate,
                OptionalId(x.DCRef), OptionalId(x.CustRef), OptionalId(x.CustCtgrRef), OptionalId(x.CustActRef),
                OptionalId(x.CustLevelRef), OptionalId(x.PaymentTypeRef), OptionalId(x.PaymentUsanceRef),
                OptionalId(x.OrderTypeRef), OptionalId(x.StateRef), OptionalId(x.CountyRef), OptionalId(x.AreaRef),
                OptionalId(x.MainCustTypeRef), OptionalId(x.SubCustTypeRef), x.Code)).ToArray());

    public async Task<ErpPage<ErpStock>> GetInventoryAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await odata.ReadAsync<StockRow>(c, ErpSettingsJson.Read<VaranegarSettings>(c.ProviderSettingsJson).InventoryPath, skip, take, ct))
            .Select(x => new ErpStock(Id(x.Id), Id(x.GoodsRef),
                x.StockDCRef is > 0 ? x.StockDCRef.Value.ToString(CultureInfo.InvariantCulture) : x.StockDCRef == 0 ? null : throw new ErpIntegrationException("invalid_warehouse"),
                x.OnHandQty ?? throw new ErpIntegrationException("missing_quantity"),
                x.CartonType, x.IsBatch ?? throw new ErpIntegrationException("missing_batch_flag"),
                ValidPrice(x.SalePrice), x.StockDCRef == 0)).ToArray());

    public async Task<ErpPage<ErpCategory>> GetCategoriesAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await odata.ReadAsync<CategoryRow>(c, ErpSettingsJson.Read<VaranegarSettings>(c.ProviderSettingsJson).CategoriesPath, skip, take, ct))
            .Select(x => new ErpCategory(Id(x.Id), OptionalId(x.ParentId), Required(x.GoodsGroupName), x.GoodsGroupPath)).ToArray());

    public async Task<ErpPage<ErpWarehouse>> GetWarehousesAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await odata.ReadAsync<WarehouseRow>(c, ErpSettingsJson.Read<VaranegarSettings>(c.ProviderSettingsJson).WarehousesPath, skip, take, ct))
            .Select(x => new ErpWarehouse(Id(x.Id), x.StockDCCode, Required(x.StockDCName), OptionalId(x.DCRef))).ToArray());

    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 400
        ? value : throw new ErpIntegrationException("invalid_required_text");
    private static string Id(int? value) => value is > 0 ? value.Value.ToString(CultureInfo.InvariantCulture)
        : throw new ErpIntegrationException("invalid_external_id");
    private static string OptionalId(int? value) => value?.ToString(CultureInfo.InvariantCulture);
    private static bool Flag(int? value) => value switch { 0 => true, 1 => false, _ => throw new ErpIntegrationException("invalid_activity_flag") };
    private static decimal? ValidPrice(decimal? value) => value is >= 0 ? value : null;
}
