using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Data;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;
using BonakMarket.PayPal.Client;
using BonakMarket.PayPal.Components;

namespace BonakMarket.PayPal.Providers;

/// <summary>
/// https://developer.paypal.com/docs/checkout/apm/google-pay/
/// </summary>
[SystemName(PayPalConstants.GooglePay)]
[FriendlyName("PayPal Google Pay")]
[Order(1)]
[PaymentMethod(PaymentMethodType.Standard | PaymentMethodType.Button)]
public class PayPalGooglePayProvider : PayPalProviderBase
{
    public PayPalGooglePayProvider(
        SmartDbContext db,
        PayPalHttpClient client,
        PayPalSettings settings,
        IPaymentService paymentService,
        ICheckoutStateAccessor checkoutStateAccessor)
        : base(db, client, settings, paymentService, checkoutStateAccessor)
    {
    }

    public override Widget GetPaymentInfoWidget()
        => new ComponentWidget(typeof(PayPalGooglePayViewComponent), true);
}