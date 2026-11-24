using BonakMarket.Admin.Models.Catalog;

namespace BonakMarket.Admin.Models.Orders;

// INFO: must be 'Admin.Catalog.Products.Fields.' for ProductOverviewModel.
[LocalizedDisplay("Admin.Catalog.Products.Fields.")]
public class BestsellersReportLineModel : ProductOverviewModel
{
    [LocalizedDisplay("Admin.SalesReport.Bestsellers.Fields.TotalAmount")]
    public Money TotalAmount { get; set; }

    [LocalizedDisplay("Admin.SalesReport.Bestsellers.Fields.TotalQuantity")]
    public string TotalQuantity { get; set; }
}