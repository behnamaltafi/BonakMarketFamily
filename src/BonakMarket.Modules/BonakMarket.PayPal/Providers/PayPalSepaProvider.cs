using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Data;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;
using BonakMarket.PayPal.Client;
using BonakMarket.PayPal.Components;

namespace BonakMarket.PayPal.Providers;

[SystemName(PayPalConstants.Sepa)]
[FriendlyName("PayPal Sepa")]
[Order(1)]
[PaymentMethod(PaymentMethodType.Standard | PaymentMethodType.Button)]
public class PayPalSepaProvider : PayPalProviderBase
{
    public PayPalSepaProvider(
        SmartDbContext db,
        PayPalHttpClient client,
        PayPalSettings settings,
        IPaymentService paymentService,
        ICheckoutStateAccessor checkoutStateAccessor)
        : base(db, client, settings, paymentService, checkoutStateAccessor)
    {
    }

    public override Widget GetPaymentInfoWidget()
        => new ComponentWidget(typeof(PayPalSepaViewComponent), true);
}