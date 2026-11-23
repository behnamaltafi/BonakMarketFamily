using BonakMarket.Core.Checkout.Orders;

namespace BonakMarket.Web.Models.Checkout;

public partial class CheckoutCompletedModel : ModelBase
{
    public Order Order { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; }
}