using System.ComponentModel.DataAnnotations;
using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Models;

public sealed class ConnectionModel
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string CompanyCode { get; set; }
    [Required, MaxLength(80)] public string Code { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; }
    [Required, MaxLength(80)] public string Provider { get; set; } = "Varanegar";
    [Required, MaxLength(500)] public string BaseUrl { get; set; }
    public bool Enabled { get; set; }
    [Range(1, 120)] public int TimeoutSeconds { get; set; } = 30;
    [MaxLength(200)] public string ODataPrefix { get; set; } = "odata/";
    [Required, MaxLength(200)] public string LoginPath { get; set; } = "api/auth/login";
    [MaxLength(40)] public string AuthorizationScheme { get; set; } = "Bearer";
    [Required, MaxLength(200)] public string ProductsPath { get; set; } = "ThirdPartyProductInfoOData";
    [Required, MaxLength(200)] public string PricesPath { get; set; } = "ThirdPartyPriceHistoryOData";
    [Required, MaxLength(200)] public string InventoryPath { get; set; } = "ThirdPartyStockProductInfoOData";
    [Required, MaxLength(200)] public string CategoriesPath { get; set; } = "ThirdPartyProductGroupInfoOData";
    [Required, MaxLength(200)] public string WarehousesPath { get; set; } = "ThirdPartyStockDCListOData";
    public bool UseLogin { get; set; } = true;
    [MaxLength(8000)] public string NewLoginBody { get; set; }
    public bool ClearLoginBody { get; set; }
    public bool HasLoginBody { get; set; }
    public string[] Providers { get; set; } = [];
}
public sealed record ErpIndexModel(IReadOnlyList<ErpConnectionListItem> Connections, IReadOnlyList<ErpSyncRun> Runs);
public sealed record ErpConnectionListItem(int Id, string CompanyCode, string Code, string Name, string Provider, bool Enabled);
public sealed record CatalogModel(int ConnectionId, int Skip, IReadOnlyList<ErpCatalogProduct> Rows, IReadOnlyList<ProductTemplateOption> Templates);
public sealed record ProductTemplateOption(int Id, string Name);
