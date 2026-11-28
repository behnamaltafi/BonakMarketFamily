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
        var from = filter.FromDateUtc?.UtcDateTime;
        var to = filter.ToDateUtc?.UtcDateTime;
        if (from.HasValue)
            query = query.Where(x => x.CreatedOnUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.CreatedOnUtc < to.Value);
        return query;
    }

    internal static IQueryable<ReturnCase> FilterReturns(IQueryable<ReturnCase> query, ReturnSearchRequest filter)
    {
        var term = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            query = int.TryParse(term, out var id) && id > 0 ? query.Where(x => x.Id == id) : query.Where(x => false);
        if (filter.StatusId.HasValue)
            query = query.Where(x => x.ReturnCaseStatusId == filter.StatusId.Value);
        var from = filter.FromDateUtc?.UtcDateTime;
        var to = filter.ToDateUtc?.UtcDateTime;
        if (from.HasValue)
            query = query.Where(x => x.CreatedOnUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.CreatedOnUtc < to.Value);
        return query;
    }

    internal static IQueryable<ProductReview> FilterReviews(IQueryable<ProductReview> query, ReviewSearchRequest filter)
    {
        var term = CatalogText.Normalize(filter.Search);
        if (term.Length > 0)
            query = query.Where(x => (x.Title ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term) ||
                (x.ReviewText ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term));
        if (filter.Rating.HasValue)
            query = query.Where(x => x.Rating == filter.Rating.Value);
        return query;
    }

    internal static IQueryable<ProductReview> FilterMyReviews(IQueryable<ProductReview> query, MyReviewSearchRequest filter)
    {
        var term = CatalogText.Normalize(filter.Search);
        if (term.Length > 0)
            query = query.Where(x => (x.Title ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term) ||
                (x.ReviewText ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term) ||
                (x.Product.Name ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term));
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
        var term = CatalogText.Normalize(filter.Search);
        if (term.Length > 0)
            query = query.Where(x => (x.Product.Name ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term));
        if (filter.OrderId.HasValue)
            query = query.Where(x => x.OrderId == filter.OrderId.Value);
        if (filter.ProductId.HasValue)
            query = query.Where(x => x.ProductId == filter.ProductId.Value);
        return query;
    }

    internal static IQueryable<BackInStockSubscription> FilterStockSubscriptions(IQueryable<BackInStockSubscription> query, StockSubscriptionSearchRequest filter)
    {
        query = query.ApplyStandardFilter(productId: filter.ProductId);
        var term = CatalogText.Normalize(filter.Search);
        if (term.Length > 0)
            query = query.Where(x => (x.Product.Name ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term));
        var from = filter.FromDateUtc?.UtcDateTime;
        var to = filter.ToDateUtc?.UtcDateTime;
        if (from.HasValue)
            query = query.Where(x => x.CreatedOnUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.CreatedOnUtc < to.Value);
        return query;
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
        var from = filter.FromDateUtc?.UtcDateTime;
        var to = filter.ToDateUtc?.UtcDateTime;
        if (from.HasValue)
            query = query.Where(x => x.CreatedOnUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.CreatedOnUtc < to.Value);
        return query;
    }

    internal static IQueryable<RewardPointsHistory> FilterRewardPoints(IQueryable<RewardPointsHistory> query, RewardPointSearchRequest filter)
    {
        var term = CatalogText.Normalize(filter.Search);
        if (term.Length > 0)
            query = query.Where(x => (x.Message ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term));
        if (filter.IsEarned.HasValue)
            query = filter.IsEarned.Value ? query.Where(x => x.Points > 0) : query.Where(x => x.Points < 0);
        var from = filter.FromDateUtc?.UtcDateTime;
        var to = filter.ToDateUtc?.UtcDateTime;
        if (from.HasValue)
            query = query.Where(x => x.CreatedOnUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.CreatedOnUtc < to.Value);
        return query;
    }

    internal static IQueryable<Topic> FilterTopics(IQueryable<Topic> query, IQueryable<LocalizedProperty> translations,
        int languageId, TopicSearchRequest filter)
    {
        var systemName = filter.SystemName?.Trim();
        if (!string.IsNullOrWhiteSpace(systemName))
            query = query.Where(x => x.SystemName == systemName);
        var term = CatalogText.Normalize(filter.Search);
        if (term.Length > 0)
        {
            var titles = translations.Where(x => x.LanguageId == languageId && x.LocaleKeyGroup == nameof(Topic) &&
                x.LocaleKey == nameof(Topic.Title) && x.LocaleValue != null && x.LocaleValue != "");
            var rows = query.Select(x => new
            {
                Topic = x,
                Title = titles.Where(t => t.EntityId == x.Id).OrderBy(t => t.Id).Select(t => t.LocaleValue).FirstOrDefault() ?? x.Title
            });
            query = rows.Where(x => (x.Title ?? "").Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "").Contains(term)).Select(x => x.Topic);
        }
        return query;
    }
}
