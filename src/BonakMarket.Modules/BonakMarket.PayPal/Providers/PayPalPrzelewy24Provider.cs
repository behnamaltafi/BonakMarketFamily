using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;
using BonakMarket.PayPal.Components;
using BonakMarket.PayPal.Services;

namespace BonakMarket.PayPal.Providers;

[SystemName(PayPalConstants.Przelewy24)]
[FriendlyName("PayPal Przelewy24")]
[Order(1)]
[PaymentMethod(PaymentMethodType.Standard)]
public class PayPalPrzelewy24Provider : PayPalApmProviderBase
{
    public PayPalPrzelewy24Provider(PayPalApmServiceContext context) : base(context)
    {
    }

    public override Widget GetPaymentInfoWidget()
        => new ComponentWidget(typeof(PayPalApmViewComponent), "p24");
}