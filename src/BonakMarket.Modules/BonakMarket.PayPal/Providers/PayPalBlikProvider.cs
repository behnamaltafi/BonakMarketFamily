using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;
using BonakMarket.PayPal.Components;
using BonakMarket.PayPal.Services;

namespace BonakMarket.PayPal.Providers;

[SystemName(PayPalConstants.Blik)]
[FriendlyName("PayPal BLIK")]
[Order(1)]
[PaymentMethod(PaymentMethodType.Standard)]
public class PayPalBlikProvider : PayPalApmProviderBase
{
    public PayPalBlikProvider(PayPalApmServiceContext context) : base(context)
    {
    }

    public override Widget GetPaymentInfoWidget()
        => new ComponentWidget(typeof(PayPalApmViewComponent), "blik");
}