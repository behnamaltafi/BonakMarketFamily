using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class ProductDetailContractTests
{
    [Test]
    public void Detail_preserves_product_summary_contract()
        => Assert.That(typeof(ProductDto).IsAssignableFrom(typeof(ProductDetailDto)), Is.True);

    [Test]
    public void Quote_is_public_catalog_and_does_not_accept_customer_or_raw_attributes()
    {
        var action = typeof(CatalogController).GetMethod(nameof(CatalogController.Quote));
        Assert.That(action.IsDefined(typeof(PublicCatalogAttribute), true), Is.True);
        Assert.That(action.GetCustomAttribute<HttpPostAttribute>().Template, Is.EqualTo("{id:int}/quote"));
        Assert.That(typeof(ProductQuoteRequest).GetProperties().Select(x => x.Name),
            Is.EquivalentTo(new[] { "Quantity", "Variants" }));
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(1000, true)]
    [TestCase(1001, false)]
    public void Quote_quantity_is_bounded(int quantity, bool expected)
    {
        var input = new ProductQuoteRequest { Quantity = quantity };
        Assert.That(Validator.TryValidateObject(input, new ValidationContext(input),
            new List<ValidationResult>(), true), Is.EqualTo(expected));
    }
}
