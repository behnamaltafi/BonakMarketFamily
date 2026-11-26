using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class OrderDetailsContractTests
{
    [TestCase(10, "pending")]
    [TestCase(20, "authorized")]
    [TestCase(30, "paid")]
    [TestCase(35, "partially_refunded")]
    [TestCase(40, "refunded")]
    [TestCase(50, "voided")]
    [TestCase(999, "unknown")]
    public void Payment_status_has_stable_code(int id, string code)
    {
        Assert.That(OrderStatusMapping.Payment(id).Code, Is.EqualTo(code));
        Assert.That(OrderStatusMapping.Payment(id).Id, Is.EqualTo(id));
    }

    [Test]
    public void Order_detail_keeps_existing_fields_and_adds_financials()
    {
        Assert.That(typeof(OrderDetailDto).GetProperties().Select(x => x.Name),
            Does.Contain("Financials").And.Contain("Order").And.Contain("Items"));
    }

    [Test]
    public void Payment_status_is_read_only()
    {
        var action = typeof(OrdersController).GetMethod(nameof(OrdersController.PaymentStatus));
        Assert.That(action.GetCustomAttributes(typeof(HttpGetAttribute), false), Is.Not.Empty);
        Assert.That(action.ReturnType, Is.EqualTo(typeof(Task<ActionResult<OrderPaymentStatusDto>>)));
    }
}
