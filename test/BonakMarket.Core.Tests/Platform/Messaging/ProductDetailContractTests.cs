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
using BonakMarket.StorefrontApi.Services;
using BonakMarket.Core.Catalog.Products;

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

    [Test]
    public void WholesaleQuantityRulesUseAllowedValuesAndApiLimit()
    {
        var range = CatalogFacade.QuantityRules(new Product { OrderMinimumQuantity = 6, OrderMaximumQuantity = 2000 });
        Assert.That(range.Min, Is.EqualTo(6));
        Assert.That(range.Max, Is.EqualTo(1000));
        var choices = CatalogFacade.QuantityRules(new Product { AllowedQuantities = "0,12,24,24,2000" });
        Assert.That(choices.Min, Is.EqualTo(12));
        Assert.That(choices.Max, Is.EqualTo(24));
        Assert.That(choices.Allowed, Is.EqualTo(new[] { 12, 24 }));
        var unsupported = CatalogFacade.QuantityRules(new Product { AllowedQuantities = "1500,2000" });
        Assert.That(unsupported.Max, Is.LessThan(unsupported.Min));
        Assert.That(unsupported.Allowed, Is.Empty);
    }

    [Test]
    public void ProductDetailsAreTheSingleVariantContract()
    {
        var routes = typeof(CatalogController).GetMethods().SelectMany(x => x.GetCustomAttributes<HttpGetAttribute>())
            .Select(x => x.Template).ToArray();
        Assert.That(routes, Does.Not.Contain("{id:int}/variants"));
        Assert.That(routes, Does.Not.Contain("{id:int}/price"));
        Assert.That(typeof(ProductDetailDto).GetProperty(nameof(ProductDetailDto.Variants)), Is.Not.Null);
        Assert.That(typeof(PriceDto).GetProperty(nameof(PriceDto.PricingQuantity)), Is.Not.Null);
    }
}
