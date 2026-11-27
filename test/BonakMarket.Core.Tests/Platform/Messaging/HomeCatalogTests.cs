using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class HomeCatalogTests
{
    private static readonly DateTime Now = new(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);

    private static Order Order(int id, int customer = 1, int store = 1,
        OrderStatus status = OrderStatus.Complete, PaymentStatus payment = PaymentStatus.Paid,
        bool deleted = false, int age = 1) => new()
    {
        Id = id, CustomerId = customer, StoreId = store, OrderStatusId = (int)status,
        PaymentStatusId = (int)payment, Deleted = deleted, CreatedOnUtc = Now.AddDays(-age)
    };

    private static OrderItem Line(int order, int product, int qty) =>
        new() { OrderId = order, ProductId = product, Quantity = qty };

    [Test]
    public void BestSellersRespectsStoreStatusTimeScopeAndStablePaging()
    {
        var orders = new[] { Order(1), Order(2, customer: 2), Order(3, store: 2),
            Order(4, status: OrderStatus.Cancelled), Order(5, payment: PaymentStatus.Refunded),
            Order(6, deleted: true), Order(7, age: 91), Order(8, status: OrderStatus.Pending),
            Order(9, status: OrderStatus.Processing, payment: PaymentStatus.Paid),
            Order(10, status: OrderStatus.Processing, payment: PaymentStatus.Pending),
            Order(11, payment: PaymentStatus.Voided) }.AsQueryable();
        var lines = new[] { Line(1, 2, 4), Line(2, 2, 3), Line(9, 3, 7), Line(1, 99, 900),
            Line(3, 1, 100), Line(4, 1, 100), Line(5, 1, 100), Line(6, 1, 100),
            Line(7, 1, 100), Line(8, 1, 100), Line(10, 1, 100), Line(11, 1, 100),
            Line(1, 1, -3) }.AsQueryable();
        var products = new[] { new Product { Id = 3 }, new Product { Id = 1 }, new Product { Id = 2 } }.AsQueryable();
        var eligible = CatalogPurchaseRanking.EligibleOrders(orders, 1, Now.AddDays(-90));
        var ranked = CatalogPurchaseRanking.BestSellers(products, CatalogPurchaseRanking.Items(lines, eligible));
        Assert.That(ranked.Select(x => x.Id).ToArray(), Is.EqualTo(new[] { 2, 3, 1 }));
        Assert.That(ranked.Skip(1).Take(1).Single().Id, Is.EqualTo(3));
    }

    [Test]
    public void FrequentRankingCountsDistinctOwnedOrdersBeforeQuantityAndExcludesInvisibleProducts()
    {
        var orders = new[] { Order(1), Order(2), Order(3, customer: 2), Order(4, store: 2), Order(5, age: 366) }
            .AsQueryable().Where(x => x.CustomerId == 1);
        var lines = new[] { Line(1, 1, 50), Line(1, 1, 40), Line(1, 2, 1), Line(2, 2, 1),
            Line(3, 3, 100), Line(4, 3, 100), Line(5, 3, 100), Line(1, 99, 100) }.AsQueryable();
        var products = new[] { new Product { Id = 1 }, new Product { Id = 2 }, new Product { Id = 3 } }.AsQueryable();
        var eligible = CatalogPurchaseRanking.EligibleOrders(orders, 1, Now.AddDays(-365));
        var ranked = CatalogPurchaseRanking.FrequentlyPurchased(products, CatalogPurchaseRanking.Items(lines, eligible));
        Assert.That(ranked.Select(x => x.Id).ToArray(), Is.EqualTo(new[] { 2, 1 }));
        Assert.That(CatalogPurchaseRanking.FrequentlyPurchased(products, Array.Empty<OrderItem>().AsQueryable()), Is.Empty);
    }

    [Test]
    public void WholesaleMetadataAndDiscountAreExplicitAndNullable()
    {
        Assert.That(WholesaleCardMetadata.PackageSize(12m), Is.EqualTo(12));
        Assert.That(WholesaleCardMetadata.PackageSize(12.5m), Is.Null);
        Assert.That(WholesaleCardMetadata.PackageSize(0m), Is.Null);
        Assert.That(WholesaleCardMetadata.PackageSize((decimal)int.MaxValue + 1), Is.Null);
        Assert.That(WholesaleCardMetadata.ConsumerPrice(100050m), Is.EqualTo(100050m));
        Assert.That(WholesaleCardMetadata.ConsumerPrice(-1m), Is.Null);
        Assert.That(WholesaleCardMetadata.CurrencyCode(" irr "), Is.EqualTo("IRR"));
        Assert.That(WholesaleCardMetadata.CurrencyCode("toman"), Is.Null);
        Assert.That(new PriceDto(1, "IRR", 80m, 100m, 20m).DiscountPercentage, Is.EqualTo(20m));
        Assert.That(new PriceDto(1, "IRR", 80m, null, 0m).DiscountPercentage, Is.Null);
    }

    [Test]
    public void FrequentActionIsPrivateAndRequiresCatalogAndOrders()
    {
        var action = typeof(CustomerServicesController).GetMethod(nameof(CustomerServicesController.FrequentlyPurchased))!;
        Assert.That(action.IsDefined(typeof(PublicCatalogAttribute), true), Is.False);
        Assert.That(action.IsDefined(typeof(AllowGuestCartAttribute), true), Is.False);
        Assert.That(action.GetCustomAttributes<StorefrontPermissionAttribute>().Select(x => x.Permission),
            Is.EquivalentTo(new[] { StorefrontPermissions.Catalog, StorefrontPermissions.Orders }));
    }
}
