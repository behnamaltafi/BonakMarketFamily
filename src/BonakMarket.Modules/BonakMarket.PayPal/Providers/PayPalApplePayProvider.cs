using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Data;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;
using BonakMarket.PayPal.Client;
using BonakMarket.PayPal.Components;

namespace BonakMarket.PayPal.Providers;

/// <summary>
/// https://developer.paypal.com/docs/checkout/apm/apple-pay/
/// </summary>
[SystemName(PayPalConstants.ApplePay)]
[FriendlyName("PayPal Apple Pay")]
[Order(2)]
[PaymentMethod(PaymentMethodType.Standard | PaymentMethodType.Button)]
public class PayPalApplePayProvider : PayPalProviderBase
{
    public PayPalApplePayProvider(
        SmartDbContext db,
        PayPalHttpClient client,
        PayPalSettings settings,
        IPaymentService paymentService,
        ICheckoutStateAccessor checkoutStateAccessor)
        : base(db, client, settings, paymentService, checkoutStateAccessor)
    {
    }

    public override Widget GetPaymentInfoWidget()
        => new ComponentWidget(typeof(PayPalApplePayViewComponent), true);
}