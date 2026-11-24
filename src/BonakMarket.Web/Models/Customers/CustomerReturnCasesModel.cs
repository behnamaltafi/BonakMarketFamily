using BonakMarket.Collections;
using BonakMarket.Core.Localization;
using BonakMarket.Web.Models.Orders;

namespace BonakMarket.Web.Models.Customers;

public partial class CustomerReturnCasesModel : ModelBase
{
    public IPagedList<CustomerReturnCaseModel> ReturnCases { get; set; }
}

public partial class CustomerReturnCaseModel : ModelBase
{
    public int ProductId { get; set; }
    public LocalizedValue<string> ProductName { get; set; }
    public string ProductSeName { get; set; }
    public string ProductUrl { get; set; }

    public ReturnCaseModel ReturnCase { get; set; }
}