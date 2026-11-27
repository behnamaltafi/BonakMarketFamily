using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using NUnit.Framework;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.StorefrontApi.Queries;

namespace BonakMarket.Core.Tests.Platform.Messaging;

/// <summary>Exercises the actual ranking expressions with the SQL Server EF provider.</summary>
[TestFixture]
public sealed class HomeCatalogSqlServerTests
{
    private const string TranslationOnlyConnection = "Server=localhost;Database=BonakMarketTranslationOnly;Integrated Security=True;TrustServerCertificate=True";

    private static RankingSqlContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<RankingSqlContext>().UseSqlServer(connectionString).Options);

    [Test]
    public void BestsellerAndPersonalRankingTranslateToGroupedSqlWithServerPaging()
    {
        using var db = CreateContext(TranslationOnlyConnection);
        var now = new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);
        var visible = db.Set<Product>().AsNoTracking().Where(p => p.Published && !p.Deleted && p.ShowOnHomePage);
        var orders = CatalogPurchaseRanking.EligibleOrders(db.Set<Order>().AsNoTracking(), 1, now.AddDays(-90));
        var lines = CatalogPurchaseRanking.Items(db.Set<OrderItem>().AsNoTracking(), orders);
        var ownedOrders = CatalogPurchaseRanking.EligibleOrders(db.Set<Order>().AsNoTracking().Where(o => o.CustomerId == 7), 1, now.AddDays(-365));
        var ownedLines = CatalogPurchaseRanking.Items(db.Set<OrderItem>().AsNoTracking(), ownedOrders);

        var bestSql = CatalogPurchaseRanking.BestSellers(visible, lines).Skip(5).Take(10).Select(p => p.Id).ToQueryString();
        var frequentSql = CatalogPurchaseRanking.FrequentlyPurchased(visible, ownedLines).Skip(5).Take(10).Select(p => p.Id).ToQueryString();

        foreach (var sql in new[] { bestSql, frequentSql })
        {
            Assert.That(sql, Does.Contain("GROUP BY").IgnoreCase);
            Assert.That(sql, Does.Contain("JOIN").IgnoreCase);
            Assert.That(sql, Does.Contain("ORDER BY").IgnoreCase);
            Assert.That(sql, Does.Contain("OFFSET").IgnoreCase);
            Assert.That(sql, Does.Contain("StoreId").IgnoreCase);
            Assert.That(sql, Does.Contain("ShowOnHomePage").IgnoreCase);
        }
        Assert.That(frequentSql, Does.Contain("DISTINCT").IgnoreCase);
        Assert.That(frequentSql, Does.Contain("CustomerId").IgnoreCase);
    }

    [Test]
    public async Task ReadOnlyRankingSmokeAgainstExplicitTestDatabase()
    {
        // Never connects to a server by default and never writes or migrates a database.
        var connectionString = Environment.GetEnvironmentVariable("BONAKMARKET_STOREFRONT_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Ignore("Supply an installed, dedicated BonakMarket SQL Server test database explicitly.");

        await using var db = CreateContext(connectionString);
        var now = DateTime.UtcNow;
        var visible = db.Set<Product>().AsNoTracking().Where(p => p.Published && !p.Deleted);
        var orders = CatalogPurchaseRanking.EligibleOrders(db.Set<Order>().AsNoTracking(), 1, now.AddDays(-90));
        var ownedOrders = CatalogPurchaseRanking.EligibleOrders(db.Set<Order>().AsNoTracking().Where(o => o.CustomerId == 7), 1, now.AddDays(-365));
        var lines = db.Set<OrderItem>().AsNoTracking();
        var bestIds = await CatalogPurchaseRanking.BestSellers(visible, CatalogPurchaseRanking.Items(lines, orders))
            .Take(10).Select(p => p.Id).ToListAsync();
        var frequentIds = await CatalogPurchaseRanking.FrequentlyPurchased(visible, CatalogPurchaseRanking.Items(lines, ownedOrders))
            .Take(10).Select(p => p.Id).ToListAsync();
        Assert.That(bestIds.Distinct().Count(), Is.EqualTo(bestIds.Count));
        Assert.That(frequentIds.Distinct().Count(), Is.EqualTo(frequentIds.Count));
    }

    // Only the columns involved in the ranking are mapped here. This keeps translation isolated from global
    // application DataSettings/EngineContext. Full ACL, mappings and execution plans also need application integration tests.
    private sealed class RankingSqlContext(DbContextOptions<RankingSqlContext> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Conventions.Remove(typeof(RelationshipDiscoveryConvention));
            configurationBuilder.Conventions.Remove(typeof(IndexAttributeConvention));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Map<Product>(modelBuilder, "Product", nameof(Product.Id), nameof(Product.Published),
                nameof(Product.Deleted), nameof(Product.ShowOnHomePage));
            Map<Order>(modelBuilder, "Order", nameof(Order.Id), nameof(Order.Deleted), nameof(Order.StoreId),
                nameof(Order.CustomerId), nameof(Order.CreatedOnUtc), nameof(Order.OrderStatusId), nameof(Order.PaymentStatusId));
            Map<OrderItem>(modelBuilder, "OrderItem", nameof(OrderItem.Id), nameof(OrderItem.OrderId),
                nameof(OrderItem.ProductId), nameof(OrderItem.Quantity), nameof(OrderItem.PriceExclTax));
        }

        private static void Map<TEntity>(ModelBuilder modelBuilder, string table, params string[] included)
            where TEntity : class
        {
            var entity = modelBuilder.Entity<TEntity>();
            var keep = new HashSet<string>(included, StringComparer.Ordinal);
            foreach (var property in typeof(TEntity).GetProperties())
            {
                if (!keep.Contains(property.Name))
                    entity.Ignore(property.Name);
            }
            entity.HasKey("Id");
            entity.ToTable(table);
        }
    }
}
