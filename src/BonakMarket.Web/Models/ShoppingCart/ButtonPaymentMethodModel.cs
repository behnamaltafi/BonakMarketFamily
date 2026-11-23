namespace BonakMarket.Web.Models.Cart;

public partial class ButtonPaymentMethodModel : ModelBase
{
    public List<Widget> Items { get; set; } = new();
}