using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Queries;

public sealed class OrderQueries(SmartDbContext db, StorefrontRequestContext request)
{
    private IQueryable<Order> Owned() => StorefrontScope.Orders(db, request.Customer, request.StoreId);
    private static IQueryable<OrderDto> Project(IQueryable<Order> query) => query.Select(x => new OrderDto
    {
        Id = x.Id, OrderNumber = x.OrderNumber, CreatedOnUtc = x.CreatedOnUtc, OrderTotal = x.OrderTotal,
        OrderStatusId = x.OrderStatusId, PaymentStatusId = x.PaymentStatusId, ShippingStatusId = x.ShippingStatusId
    });

    public Task<PageDto<OrderDto>> ListAsync(OrderSearchRequest filter, CancellationToken ct)
    {
        var query = Owned().AsNoTracking().ApplyStatusFilter(
            filter.OrderStatusId.HasValue ? new[] { filter.OrderStatusId.Value } : null,
            filter.PaymentStatusId.HasValue ? new[] { filter.PaymentStatusId.Value } : null,
            filter.ShippingStatusId.HasValue ? new[] { filter.ShippingStatusId.Value } : null);
        return Project(query.OrderByDescending(x => x.CreatedOnUtc).ThenByDescending(x => x.Id)).PageAsync(filter, ct);
    }

    public async Task<OrderDetailDto> DetailAsync(int id, CancellationToken ct)
    {
        var owned = Owned().AsNoTracking().Where(x => x.Id == id);
        var order = await Project(owned).FirstOrDefaultAsync(ct);
        if (order == null) return null;
        var ids = owned.Select(x => x.Id);
        var items = await db.Set<OrderItem>().AsNoTracking().Where(x => ids.Contains(x.OrderId))
            .Select(x => new OrderLineDto(x.Id, x.ProductId, x.Quantity, x.UnitPriceInclTax, x.PriceInclTax)).ToListAsync(ct);
        return new OrderDetailDto(order, items);
    }

    public Task<Order> FindOwnedAsync(int id, CancellationToken ct) => Owned().FirstOrDefaultAsync(x => x.Id == id, ct);
}
