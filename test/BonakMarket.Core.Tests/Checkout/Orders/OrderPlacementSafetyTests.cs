using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;

namespace BonakMarket.Core.Tests.Checkout.Orders;

[TestFixture]
public class OrderPlacementSafetyTests
{
    [Test]
    public void Existing_callers_without_expected_amount_remain_compatible()
    {
        var request = new ProcessPaymentRequest();
        Assert.That(request.ExpectedOrderTotal, Is.Null);
        Assert.That(request.MatchesExpectedOrderTotal(123.45m), Is.True);
    }

    [Test]
    public void Expected_amount_is_an_exact_decimal_precondition_not_the_charge()
    {
        var request = new ProcessPaymentRequest { ExpectedOrderTotal = 123.45m, OrderTotal = 123.46m };
        Assert.That(request.MatchesExpectedOrderTotal(request.OrderTotal), Is.False);
        Assert.That(request.MatchesExpectedOrderTotal(123.45m), Is.True);
        Assert.That(request.MatchesExpectedOrderTotal(123.450m), Is.True);
        Assert.That(request.MatchesExpectedOrderTotal(123.44m), Is.False);
        Assert.That(request.OrderTotal, Is.EqualTo(123.46m));
    }

    [Test]
    public void Zero_expected_total_is_not_treated_as_unspecified()
    {
        var request = new ProcessPaymentRequest { ExpectedOrderTotal = 0m };
        Assert.That(request.MatchesExpectedOrderTotal(0m), Is.True);
        Assert.That(request.MatchesExpectedOrderTotal(0.01m), Is.False);
    }

    [Test]
    public void Order_guid_model_requires_unique_index()
    {
        var index = typeof(Order).GetCustomAttributes(typeof(IndexAttribute), false)
            .Cast<IndexAttribute>().Single(x => x.PropertyNames.SequenceEqual(new[] { nameof(Order.OrderGuid) }));
        Assert.That(index.Name, Is.EqualTo("UX_Order_OrderGuid"));
        Assert.That(index.IsUnique, Is.True);
    }
}
