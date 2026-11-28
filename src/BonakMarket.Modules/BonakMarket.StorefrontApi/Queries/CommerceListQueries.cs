using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Content.Topics;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Localization;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Queries;

// These filters only narrow already authorized queries. Keep all composition in SQL before paging.
internal static class CommerceListQueries
{
    internal static IQueryable<Order> FilterOrders(IQueryable<Order> query, OrderSearchRequest filter)
    {
        query = query.ApplyOrderNumberFilter(filter.Search?.Trim());
        return ApplyDateRange(query, filter, x => x.CreatedOnUtc);
    }

    internal static IQueryable<ReturnCase> FilterReturns(IQueryable<ReturnCase> query, ReturnSearchRequest filter)
    {
        var term = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            query = int.TryParse(term, out var id) && id > 0 ? query.Where(x => x.Id == id) : query.Where(x => false);
        if (filter.StatusId.HasValue)
            query = query.Where(x => x.ReturnCaseStatusId == filter.StatusId.Value);
        return ApplyDateRange(query, filter, x => x.CreatedOnUtc);
    }

    internal static IQueryable<ProductReview> FilterReviews(IQueryable<ProductReview> query, ReviewSearchRequest filter)
    {
        query = CatalogText.WhereNormalizedContains(query, filter.Search, x => x.Title, x => x.ReviewText);
        if (filter.Rating.HasValue)
            query = query.Where(x => x.Rating == filter.Rating.Value);
        return query;
    }

    internal static IQueryable<ProductReview> FilterMyReviews(IQueryable<ProductReview> query, MyReviewSearchRequest filter)
    {
        query = CatalogText.WhereNormalizedContains(query, filter.Search, x => x.Title, x => x.ReviewText, x => x.Product.Name);
        if (filter.Rating.HasValue)
            query = query.Where(x => x.Rating == filter.Rating.Value);
        if (filter.ProductId.HasValue)
            query = query.Where(x => x.ProductId == filter.ProductId.Value);
        if (filter.IsApproved.HasValue)
            query = query.Where(x => x.IsApproved == filter.IsApproved.Value);
        return query;
    }

    internal static IQueryable<OrderItem> FilterDownloads(IQueryable<OrderItem> query, DownloadSearchRequest filter)
    {
        query = CatalogText.WhereNormalizedContains(query, filter.Search, x => x.Product.Name);
        if (filter.OrderId.HasValue)
            query = query.Where(x => x.OrderId == filter.OrderId.Value);
        if (filter.ProductId.HasValue)
            query = query.Where(x => x.ProductId == filter.ProductId.Value);
        return query;
    }

    internal static IQueryable<BackInStockSubscription> FilterStockSubscriptions(IQueryable<BackInStockSubscription> query, StockSubscriptionSearchRequest filter)
    {
        query = query.ApplyStandardFilter(productId: filter.ProductId);
        query = CatalogText.WhereNormalizedContains(query, filter.Search, x => x.Product.Name);
        return ApplyDateRange(query, filter, x => x.CreatedOnUtc);
    }

    internal static IQueryable<RecurringPayment> FilterRecurringPayments(IQueryable<RecurringPayment> query,
        IQueryable<Order> ownedOrders, RecurringPaymentSearchRequest filter)
    {
        var term = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var matchingOrders = ownedOrders.ApplyOrderNumberFilter(term);
            query = query.Where(x => matchingOrders.Any(o => o.Id == x.InitialOrderId));
        }
        if (filter.IsActive.HasValue)
            query = query.Where(x => x.IsActive == filter.IsActive.Value);
        return ApplyDateRange(query, filter, x => x.CreatedOnUtc);
    }

    internal static IQueryable<RewardPointsHistory> FilterRewardPoints(IQueryable<RewardPointsHistory> query, RewardPointSearchRequest filter)
    {
        query = CatalogText.WhereNormalizedContains(query, filter.Search, x => x.Message);
        if (filter.IsEarned.HasValue)
            query = filter.IsEarned.Value ? query.Where(x => x.Points > 0) : query.Where(x => x.Points < 0);
        return ApplyDateRange(query, filter, x => x.CreatedOnUtc);
    }

    internal static IQueryable<Topic> FilterTopics(IQueryable<Topic> query, IQueryable<LocalizedProperty> translations,
        int languageId, bool useLocalizedTitle, TopicSearchRequest filter)
    {
        var systemName = filter.SystemName?.Trim();
        if (!string.IsNullOrWhiteSpace(systemName))
            query = query.Where(x => x.SystemName == systemName);
        if (!useLocalizedTitle)
            return CatalogText.WhereNormalizedContains(query, filter.Search, x => x.Title);

        if (CatalogText.Normalize(filter.Search).Length > 0)
        {
            var titles = translations.ApplyStandardFilter(languageId, 0, nameof(Topic), nameof(Topic.Title))
                .Where(x => x.LocaleValue != null && x.LocaleValue != "");
            var rows = query.Select(x => new
            {
                Topic = x,
                Title = titles.Where(t => t.EntityId == x.Id).OrderBy(t => t.Id).Select(t => t.LocaleValue).FirstOrDefault() ?? x.Title
            });
            query = CatalogText.WhereNormalizedContains(rows, filter.Search, x => x.Title).Select(x => x.Topic);
        }
        return query;
    }

    private static IQueryable<T> ApplyDateRange<T>(IQueryable<T> query, DatedSearchPageRequest filter,
        Expression<Func<T, DateTime>> date)
    {
        if (filter.FromDateUtc.HasValue)
        {
            var from = filter.FromDateUtc.Value.UtcDateTime;
            Expression<Func<DateTime, bool>> predicate = value => value >= from;
            query = query.Where(Expression.Lambda<Func<T, bool>>(
                ReplacingExpressionVisitor.Replace(predicate.Parameters[0], date.Body, predicate.Body), date.Parameters));
        }

        if (filter.ToDateUtc.HasValue)
        {
            var to = filter.ToDateUtc.Value.UtcDateTime;
            Expression<Func<DateTime, bool>> predicate = value => value < to;
            query = query.Where(Expression.Lambda<Func<T, bool>>(
                ReplacingExpressionVisitor.Replace(predicate.Parameters[0], date.Body, predicate.Body), date.Parameters));
        }

        return query;
    }
}
