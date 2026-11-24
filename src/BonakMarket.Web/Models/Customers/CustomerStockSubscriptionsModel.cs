using BonakMarket.Collections;
using BonakMarket.Core.Localization;

namespace BonakMarket.Web.Models.Customers;

public partial class CustomerStockSubscriptionsModel : PagedListBase
{
    public CustomerStockSubscriptionsModel(IPageable pageable) : base(pageable)
    {
        Subscriptions = new List<StockSubscriptionModel>();
    }

    public List<StockSubscriptionModel> Subscriptions { get; set; } = new();
}

public partial class StockSubscriptionModel : EntityModelBase
{
    public int ProductId { get; set; }
    public LocalizedValue<string> ProductName { get; set; }
    public string SeName { get; set; }
}