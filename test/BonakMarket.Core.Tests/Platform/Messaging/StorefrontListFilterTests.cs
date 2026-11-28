using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using NUnit.Framework;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public sealed class StorefrontListFilterTests
{
    [TestCase(typeof(OrderSearchRequest))]
    [TestCase(typeof(ReturnSearchRequest))]
    [TestCase(typeof(ReviewSearchRequest))]
    [TestCase(typeof(MyReviewSearchRequest))]
    [TestCase(typeof(DownloadSearchRequest))]
    [TestCase(typeof(StockSubscriptionSearchRequest))]
    [TestCase(typeof(RecurringPaymentSearchRequest))]
    [TestCase(typeof(RewardPointSearchRequest))]
    [TestCase(typeof(TopicSearchRequest))]
    [TestCase(typeof(ProductSearchRequest))]
    [TestCase(typeof(CategorySearchRequest))]
    [TestCase(typeof(BrandSearchRequest))]
    public void ListsSharePagingAndSearchValidation(Type requestType)
    {
        var request = (SearchPageRequest)Activator.CreateInstance(requestType);
        Assert.That(request.Page, Is.EqualTo(1));
        Assert.That(request.PageSize, Is.EqualTo(20));
        request.Search = new string('x', 201);
        Assert.That(IsValid(request), Is.False);
        request.Search = null;
        request.PageSize = 101;
        Assert.That(IsValid(request), Is.False);
    }

    [Test]
    public void DateRangeRejectsEqualOrReversedInstants()
    {
        var from = DateTimeOffset.Parse("2026-11-01T03:30:00+03:30");
        var request = new OrderSearchRequest { FromDateUtc = from, ToDateUtc = from.ToUniversalTime() };
        Assert.That(IsValid(request), Is.False);
        request.ToDateUtc = from.AddDays(-1);
        Assert.That(IsValid(request), Is.False);
        request.ToDateUtc = from.AddDays(1);
        Assert.That(IsValid(request), Is.True);
    }

    [Test]
    public void OrdersCombineNativeExactNumberAndUtcDateRange()
    {
        var day = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            new Order { Id = 1, OrderNumber = "BM-10", CreatedOnUtc = day },
            new Order { Id = 2, OrderNumber = "BM-10", CreatedOnUtc = day.AddDays(1) },
            new Order { Id = 3, OrderNumber = "BM-100", CreatedOnUtc = day }
        }.AsQueryable();
        var filter = new OrderSearchRequest { Search = "BM-10", FromDateUtc = day, ToDateUtc = day.AddDays(1) };
        Assert.That(CommerceListQueries.FilterOrders(rows, filter).Select(x => x.Id), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void ReviewSearchNormalizesPersianAndCombinesRating()
    {
        var rows = new[]
        {
            new ProductReview { Id = 1, Title = "كيفيت خوب", Rating = 5 },
            new ProductReview { Id = 2, Title = "کیفیت خوب", Rating = 2 },
            new ProductReview { Id = 3, Title = "other", Rating = 5 }
        }.AsQueryable();
        var filter = new ReviewSearchRequest { Search = "کیفیت", Rating = 5 };
        Assert.That(CommerceListQueries.FilterReviews(rows, filter).Select(x => x.Id), Is.EqualTo(new[] { 1 }));
        filter.Rating = 6;
        Assert.That(IsValid(filter), Is.False);
    }

    [Test]
    public void RewardDirectionAndSearchOnlyNarrowTheProvidedScope()
    {
        var rows = new[]
        {
            new RewardPointsHistory { Id = 1, CustomerId = 7, Points = 10, Message = "order" },
            new RewardPointsHistory { Id = 2, CustomerId = 7, Points = -10, Message = "order" },
            new RewardPointsHistory { Id = 3, CustomerId = 8, Points = 10, Message = "order" }
        }.AsQueryable().Where(x => x.CustomerId == 7);
        var filter = new RewardPointSearchRequest { Search = "order", IsEarned = true };
        Assert.That(CommerceListQueries.FilterRewardPoints(rows, filter).Select(x => x.Id), Is.EqualTo(new[] { 1 }));
        filter.IsEarned = false;
        Assert.That(CommerceListQueries.FilterRewardPoints(rows, filter).Select(x => x.Id), Is.EqualTo(new[] { 2 }));
    }

    [Test]
    public void ReturnsCombineExactIdAndStatus()
    {
        var rows = new[] { new ReturnCase { Id = 5, ReturnCaseStatusId = 10 },
            new ReturnCase { Id = 6, ReturnCaseStatusId = 20 } }.AsQueryable();
        var filter = new ReturnSearchRequest { Search = "5", StatusId = 10 };
        Assert.That(CommerceListQueries.FilterReturns(rows, filter).Select(x => x.Id), Is.EqualTo(new[] { 5 }));
        filter.Search = "invalid";
        Assert.That(CommerceListQueries.FilterReturns(rows, filter), Is.Empty);
    }

    private static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true);
}
