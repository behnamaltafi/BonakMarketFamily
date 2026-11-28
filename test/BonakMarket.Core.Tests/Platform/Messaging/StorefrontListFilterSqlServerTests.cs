using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using NUnit.Framework;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Content.Topics;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Localization;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;

namespace BonakMarket.Core.Tests.Platform.Messaging;

/// <summary>Checks the real storefront predicates against the SQL Server query translator without a database connection.</summary>
[TestFixture]
public sealed class StorefrontListFilterSqlServerTests
{
    [Test]
    public void AllNineCollectionFiltersTranslateBeforePagination()
    {
        using var db = new ListFilterSqlContext(new DbContextOptionsBuilder<ListFilterSqlContext>()
            .UseSqlServer("Server=localhost;Database=BonakMarketTranslationOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        var from = DateTimeOffset.Parse("2026-11-01T03:30:00+03:30");
        var to = from.AddDays(1);
        var orders = db.Set<Order>().Where(x => x.CustomerId == 7 && x.StoreId == 1);
        var reviews = db.Set<ProductReview>().Where(x => x.CustomerId == 7);
        var queries = new[]
        {
            Paged(CommerceListQueries.FilterOrders(orders, new OrderSearchRequest { Search = "BM-10", FromDateUtc = from, ToDateUtc = to }).Select(x => x.Id)),
            Paged(CommerceListQueries.FilterReturns(db.Set<ReturnCase>().Where(x => x.CustomerId == 7),
                new ReturnSearchRequest { Search = "5", StatusId = 10, FromDateUtc = from, ToDateUtc = to }).Select(x => x.Id)),
            Paged(CommerceListQueries.FilterReviews(reviews.Where(x => x.IsApproved), new ReviewSearchRequest { Search = "کیفیت", Rating = 5 }).Select(x => x.Id)),
            Paged(CommerceListQueries.FilterMyReviews(reviews, new MyReviewSearchRequest { Search = "کیفیت", Rating = 5, IsApproved = false, ProductId = 1 }).Select(x => x.Id)),
            Paged(CommerceListQueries.FilterDownloads(db.Set<OrderItem>(), new DownloadSearchRequest { Search = "شیر", OrderId = 1, ProductId = 1 }).Select(x => x.Id)),
            Paged(CommerceListQueries.FilterStockSubscriptions(db.Set<BackInStockSubscription>().Where(x => x.CustomerId == 7),
                new StockSubscriptionSearchRequest { Search = "شیر", ProductId = 1, FromDateUtc = from, ToDateUtc = to }).Select(x => x.Id)),
            Paged(CommerceListQueries.FilterRecurringPayments(db.Set<RecurringPayment>(), orders,
                new RecurringPaymentSearchRequest { Search = "BM-10", IsActive = true, FromDateUtc = from, ToDateUtc = to }).Select(x => x.Id)),
            Paged(CommerceListQueries.FilterRewardPoints(db.Set<RewardPointsHistory>().Where(x => x.CustomerId == 7),
                new RewardPointSearchRequest { Search = "خرید", IsEarned = true, FromDateUtc = from, ToDateUtc = to }).Select(x => x.Id)),
            Paged(CommerceListQueries.FilterTopics(db.Set<Topic>().Where(x => x.IsPublished), db.Set<LocalizedProperty>(),
                1, true, new TopicSearchRequest { Search = "تماس", SystemName = "ContactUs" }).Select(x => x.Id))
        };

        foreach (var sql in queries)
        {
            Assert.That(sql, Does.Contain("WHERE").IgnoreCase);
            Assert.That(sql, Does.Contain("OFFSET").IgnoreCase);
            Assert.That(sql, Does.Contain("ORDER BY").IgnoreCase);
        }
        Assert.That(queries[0], Does.Contain("CreatedOnUtc").IgnoreCase);
        Assert.That(queries[3], Does.Contain("Product").IgnoreCase);
        Assert.That(queries[7], Does.Contain("REPLACE").IgnoreCase);
        Assert.That(queries[8], Does.Contain("LocalizedProperty").IgnoreCase);
    }

    private static string Paged(IQueryable<int> query) => query.OrderBy(x => x).Skip(1).Take(5).ToQueryString();

    private sealed class ListFilterSqlContext(DbContextOptions<ListFilterSqlContext> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Conventions.Remove(typeof(RelationshipDiscoveryConvention));
            configurationBuilder.Conventions.Remove(typeof(IndexAttributeConvention));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Map<Product>(modelBuilder, "Product", "Id", "Name");
            Map<Order>(modelBuilder, "Order", "Id", "CustomerId", "StoreId", "OrderNumber", "OrderGuid", "CreatedOnUtc");
            Map<ReturnCase>(modelBuilder, "ReturnCase", "Id", "CustomerId", "ReturnCaseStatusId", "CreatedOnUtc");
            Map<ProductReview>(modelBuilder, "ProductReview", "Id", "CustomerId", "ProductId", "Title", "ReviewText", "Rating", "IsApproved", "Product");
            Map<OrderItem>(modelBuilder, "OrderItem", "Id", "OrderId", "ProductId", "Product");
            Map<BackInStockSubscription>(modelBuilder, "BackInStockSubscription", "Id", "CustomerId", "ProductId", "CreatedOnUtc", "Product");
            Map<RecurringPayment>(modelBuilder, "RecurringPayment", "Id", "InitialOrderId", "CreatedOnUtc", "IsActive");
            Map<RewardPointsHistory>(modelBuilder, "RewardPointsHistory", "Id", "CustomerId", "Points", "Message", "CreatedOnUtc");
            Map<Topic>(modelBuilder, "Topic", "Id", "IsPublished", "SystemName", "Title");
            Map<LocalizedProperty>(modelBuilder, "LocalizedProperty", "Id", "EntityId", "LanguageId", "LocaleKeyGroup", "LocaleKey", "LocaleValue");
            modelBuilder.Entity<ProductReview>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            modelBuilder.Entity<OrderItem>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            modelBuilder.Entity<BackInStockSubscription>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
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
