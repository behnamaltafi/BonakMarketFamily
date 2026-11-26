namespace BonakMarket.ERP.Providers.Varanegar;

// The wire models deliberately retain the exact names and nullable numeric
// values of the supplier document. Missing critical fields fail validation.
internal sealed class ProductRow
{
    public ProductRow() { }
    public int? Id { get; set; }
    public string GoodsCode { get; set; }
    public string GoodsName { get; set; }
    public int? BrandRef { get; set; }
    public string BrandName { get; set; }
    public int? ManufacturerRef { get; set; }
    public string ManufacturerName { get; set; }
    public int? GoodsGroupRef { get; set; }
    public string UnitName { get; set; }
    public string PackUnitName { get; set; }
    public decimal? CartonType { get; set; }
    public string DefaultBarcode { get; set; }
    public string Barcode1 { get; set; }
    public string GTIN { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? UserPrice { get; set; }
    public decimal? ManufacturerPrice { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Charge { get; set; }
    public string ModifiedDate { get; set; }
    public int? NoSaleFlag { get; set; }
    public int? NoStockFlag { get; set; }
}
internal sealed class PriceRow
{
    public PriceRow() { }
    public int? Id { get; set; }
    public int? GoodsRef { get; set; }
    public int? Code { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? UserPrice { get; set; }
    public decimal? ManufacturerPrice { get; set; }
    public int? DCRef { get; set; }
    public int? CustRef { get; set; }
    public int? CustCtgrRef { get; set; }
    public int? CustActRef { get; set; }
    public int? CustLevelRef { get; set; }
    public int? PaymentTypeRef { get; set; }
    public int? PaymentUsanceRef { get; set; }
    public int? OrderTypeRef { get; set; }
    public int? StateRef { get; set; }
    public int? CountyRef { get; set; }
    public int? AreaRef { get; set; }
    public int? MainCustTypeRef { get; set; }
    public int? SubCustTypeRef { get; set; }
}
internal sealed class StockRow
{
    public StockRow() { }
    public int? Id { get; set; }
    public int? GoodsRef { get; set; }
    public int? StockDCRef { get; set; }
    public decimal? OnHandQty { get; set; }
    public decimal? CartonType { get; set; }
    public bool? IsBatch { get; set; }
    public decimal? SalePrice { get; set; }
}
internal sealed class CategoryRow
{
    public CategoryRow() { }
    public int? Id { get; set; }
    public int? ParentId { get; set; }
    public string GoodsGroupName { get; set; }
    public string GoodsGroupPath { get; set; }
}
internal sealed class WarehouseRow
{
    public WarehouseRow() { }
    public int? Id { get; set; }
    public string StockDCCode { get; set; }
    public string StockDCName { get; set; }
    public int? DCRef { get; set; }
}
