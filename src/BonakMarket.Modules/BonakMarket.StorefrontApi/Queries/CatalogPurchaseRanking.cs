using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;

namespace BonakMarket.StorefrontApi.Queries;

/// <summary>Read-only rankings. Caller supplies an ACL/store-scoped product query and, for personal rankings, owned orders.</summary>
internal static class CatalogPurchaseRanking
{
    internal static IQueryable<Order> EligibleOrders(IQueryable<Order> orders, int storeId, DateTime sinceUtc) =>
        orders.Where(x => !x.Deleted && x.StoreId == storeId && x.CreatedOnUtc >= sinceUtc &&
            x.PaymentStatusId != (int)PaymentStatus.Refunded && x.PaymentStatusId != (int)PaymentStatus.Voided &&
            (x.OrderStatusId == (int)OrderStatus.Complete ||
                (x.OrderStatusId == (int)OrderStatus.Processing && x.PaymentStatusId == (int)PaymentStatus.Paid)));

    internal static IQueryable<OrderItem> Items(IQueryable<OrderItem> lines, IQueryable<Order> orders) =>
        lines.Where(x => x.Quantity > 0 && orders.Any(o => o.Id == x.OrderId));

    internal static IQueryable<Product> BestSellers(IQueryable<Product> products, IQueryable<OrderItem> lines) =>
        products.OrderByDescending(p => lines.Where(x => x.ProductId == p.Id).Sum(x => (long?)x.Quantity) ?? 0L)
            .ThenBy(p => p.Id);

    internal static IQueryable<Product> FrequentlyPurchased(IQueryable<Product> products, IQueryable<OrderItem> lines) =>
        products.Where(p => lines.Any(x => x.ProductId == p.Id))
            .OrderByDescending(p => lines.Where(x => x.ProductId == p.Id).Select(x => x.OrderId).Distinct().Count())
            .ThenByDescending(p => lines.Where(x => x.ProductId == p.Id).Sum(x => (long?)x.Quantity) ?? 0L)
            .ThenBy(p => p.Id);
}
