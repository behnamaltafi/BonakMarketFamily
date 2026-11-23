using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Data;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;
using BonakMarket.PayPal.Client;
using BonakMarket.PayPal.Components;

namespace BonakMarket.PayPal.Providers;

[SystemName(PayPalConstants.Standard)]
[FriendlyName("PayPal Standard")]
[Order(1)]
[PaymentMethod(PaymentMethodType.Standard | PaymentMethodType.Button)]
public class PayPalStandardProvider : PayPalProviderBase
{
    public PayPalStandardProvider(
        SmartDbContext db,
        PayPalHttpClient client,
        PayPalSettings settings,
        IPaymentService paymentService,
        ICheckoutStateAccessor checkoutStateAccessor)
        : base(db, client, settings, paymentService, checkoutStateAccessor)
    {
    }

    public override Widget GetPaymentInfoWidget()
        => new ComponentWidget(typeof(PayPalViewComponent), true);
}