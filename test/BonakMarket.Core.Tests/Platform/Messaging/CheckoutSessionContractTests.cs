using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class CheckoutSessionContractTests
{
    [Test]
    public void Draft_session_actions_are_authenticated_and_return_typed_state()
    {
        var type = typeof(CheckoutSessionsController);
        Assert.That(type.GetMethod(nameof(CheckoutSessionsController.Start)).ReturnType,
            Is.EqualTo(typeof(Task<ActionResult<CheckoutSessionDto>>)));
        foreach (var method in new[] { "Start", "Current", "Get", "SetAddresses", "ShippingOptions", "SetShipping", "PaymentMethods", "SetPayment" })
            Assert.That(type.GetMethod(method).IsDefined(typeof(PublicCatalogAttribute), true), Is.False, method);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    public void Address_selection_requires_revision(int revision, bool expected)
    {
        var input = new CheckoutAddressesRequest { Revision = revision, BillingAddressId = 1 };
        Assert.That(Validator.TryValidateObject(input, new ValidationContext(input),
            new List<ValidationResult>(), true), Is.EqualTo(expected));
    }

    [Test]
    public void Payment_selection_does_not_accept_customer_or_payment_credentials()
        => Assert.That(typeof(CheckoutPaymentSelectionRequest).GetProperties().Select(x => x.Name),
            Is.EquivalentTo(new[] { "Revision", "SystemName" }));

    [Test]
    public void Shipping_rate_is_a_required_precondition()
    {
        var input = new CheckoutShippingSelectionRequest { Revision = 1, Provider = "Provider", ShippingMethodId = 1 };
        Assert.That(Validator.TryValidateObject(input, new ValidationContext(input),
            new List<ValidationResult>(), true), Is.False);
        input.ObservedRate = 0;
        Assert.That(Validator.TryValidateObject(input, new ValidationContext(input),
            new List<ValidationResult>(), true), Is.True);
    }
}
