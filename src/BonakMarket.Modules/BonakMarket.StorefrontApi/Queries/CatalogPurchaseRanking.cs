using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Orders.Reporting;
using BonakMarket;
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

    internal static IQueryable<Product> BestSellers(IQueryable<Product> products, IQueryable<OrderItem> lines)
    {
        // Reuse the same native aggregate as the admin report and homepage component.
        var report = lines.SelectAsBestsellersReportLine(ReportSorting.ByQuantityDesc);
        return from product in products
               join row in report on product.Id equals row.ProductId
               orderby row.TotalQuantity descending, product.Id
               select product;
    }

    internal static IQueryable<Product> FrequentlyPurchased(IQueryable<Product> products, IQueryable<OrderItem> lines)
    {
        // Native bestsellers aggregate counts units, not distinct orders; this customer-specific metric is an extension.
        var report = lines.GroupBy(x => x.ProductId).Select(g => new
        {
            ProductId = g.Key,
            OrderCount = g.Select(x => x.OrderId).Distinct().Count(),
            Quantity = g.Sum(x => (long)x.Quantity)
        });
        return from product in products
               join row in report on product.Id equals row.ProductId
               orderby row.OrderCount descending, row.Quantity descending, product.Id
               select product;
    }
}
