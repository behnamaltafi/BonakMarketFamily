using System;
using System.Threading.Tasks;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Widgets;

namespace BonakMarket.Core.Tests.Checkout.Payment;

[PaymentMethod(PaymentMethodType.Standard)]
public class TestPaymentMethod2 : PaymentMethodBase
{
    public override Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var result = new ProcessPaymentResult
        {
            NewPaymentStatus = PaymentStatus.Paid
        };

        return Task.FromResult(result);
    }

    public override bool SupportCapture
        => false;

    public override bool SupportPartiallyRefund
        => false;

    public override bool SupportRefund
        => false;

    public override bool SupportVoid
        => false;

    public override Widget GetPaymentInfoWidget()
        => throw new NotImplementedException();
}