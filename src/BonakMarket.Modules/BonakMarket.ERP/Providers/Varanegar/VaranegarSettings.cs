namespace BonakMarket.ERP.Providers.Varanegar;
public sealed class VaranegarSettings
{
    public string ODataPrefix { get; set; } = "odata/";
    public string ProductsPath { get; set; } = "ThirdPartyProductInfoOData";
    public string PricesPath { get; set; } = "ThirdPartyPriceHistoryOData";
    public string InventoryPath { get; set; } = "ThirdPartyStockProductInfoOData";
    public string CategoriesPath { get; set; } = "ThirdPartyProductGroupInfoOData";
    public string WarehousesPath { get; set; } = "ThirdPartyStockDCListOData";
}
