using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Common;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Queries;

public sealed class OrderQueries(SmartDbContext db, StorefrontRequestContext request, IMediaService media)
{
    private IQueryable<Order> Owned() => StorefrontScope.Orders(db, request.Customer, request.StoreId);
    private static IQueryable<OrderDto> Project(IQueryable<Order> query) => query.Select(x => new OrderDto
    {
        Id = x.Id, OrderNumber = x.OrderNumber, CreatedOnUtc = x.CreatedOnUtc, OrderTotal = x.OrderTotal,
        OrderStatusId = x.OrderStatusId, PaymentStatusId = x.PaymentStatusId, ShippingStatusId = x.ShippingStatusId,
        CustomerCurrencyCode = x.CustomerCurrencyCode, CurrencyRate = x.CurrencyRate, PaidDateUtc = x.PaidDateUtc,
        ItemCount = x.OrderItems.Count, TotalQuantity = x.OrderItems.Sum(y => (int?)y.Quantity) ?? 0
    });

    public async Task<PageDto<OrderDto>> ListAsync(OrderSearchRequest filter, CancellationToken ct)
    {
        var query = Owned().AsNoTracking().ApplyStatusFilter(
            filter.OrderStatusId.HasValue ? new[] { filter.OrderStatusId.Value } : null,
            filter.PaymentStatusId.HasValue ? new[] { filter.PaymentStatusId.Value } : null,
            filter.ShippingStatusId.HasValue ? new[] { filter.ShippingStatusId.Value } : null);
        query = CommerceListQueries.FilterOrders(query, filter);
        var page = await Project(query.OrderByDescending(x => x.CreatedOnUtc).ThenByDescending(x => x.Id)).PageAsync(filter, ct);
        var ids = page.Items.Select(x => x.Id).ToArray();
        // Select at most four distinct previews per order; no per-order SQL query.
        var allRows = await db.Set<OrderItem>().AsNoTracking().Where(x => ids.Contains(x.OrderId))
            .Select(x => new { x.OrderId, x.ProductId }).Distinct().ToListAsync(ct);
        var rows = allRows.GroupBy(x => x.OrderId).SelectMany(g => g.OrderBy(x => x.ProductId).Take(4)).ToList();
        var previews = await CurrentProductsAsync(rows.Select(x => x.ProductId), ct);
        var grouped = rows.ToLookup(x => x.OrderId);
        foreach (var order in page.Items)
            order.PreviewProducts = grouped[order.Id].OrderBy(x => x.ProductId)
                .Select(x => previews.GetValueOrDefault(x.ProductId) ?? new OrderProductPreviewDto(x.ProductId, null, null)).ToArray();
        return page;
    }

    public async Task<OrderDetailDto> DetailAsync(int id, CancellationToken ct)
    {
        var owned = Owned().AsNoTracking().Where(x => x.Id == id);
        var order = await Project(owned).FirstOrDefaultAsync(ct);
        if (order == null) return null;
        var entity = await owned.Include(x => x.BillingAddress).ThenInclude(x => x.Country)
            .Include(x => x.BillingAddress).ThenInclude(x => x.StateProvince)
            .Include(x => x.ShippingAddress).ThenInclude(x => x.Country)
            .Include(x => x.ShippingAddress).ThenInclude(x => x.StateProvince)
            .FirstOrDefaultAsync(ct);
        if (entity == null) return null;
        var ids = owned.Select(x => x.Id);
        var items = await db.Set<OrderItem>().AsNoTracking().Where(x => ids.Contains(x.OrderId))
            .OrderBy(x => x.Id)
            .Select(x => new OrderLineDto(x.Id, x.ProductId, x.Quantity, x.UnitPriceInclTax, x.PriceInclTax)
            {
                Sku = x.Sku, AttributeDescriptionHtml = x.AttributeDescription, UnitPriceExclTax = x.UnitPriceExclTax,
                PriceExclTax = x.PriceExclTax, DiscountAmountInclTax = x.DiscountAmountInclTax,
                DiscountAmountExclTax = x.DiscountAmountExclTax, TaxRate = x.TaxRate
            }).ToListAsync(ct);
        var previews = await CurrentProductsAsync(items.Select(x => x.ProductId), ct);
        items = items.Select(x => previews.TryGetValue(x.ProductId, out var product)
            ? x with { CurrentProductName = product.Name, CurrentImageUrl = product.ImageUrl } : x).ToList();
        order.PreviewProducts = items.Select(x => x.ProductId).Distinct().Take(4)
            .Select(x => previews.GetValueOrDefault(x) ?? new OrderProductPreviewDto(x, null, null)).ToArray();

        var giftAmount = await owned.SelectMany(x => x.GiftCardUsageHistory).SumAsync(x => (decimal?)x.UsedValue, ct) ?? 0m;
        var shipments = await db.Shipments.AsNoTracking().Where(x => ids.Contains(x.OrderId)).OrderBy(x => x.Id)
            .Select(x => new ShipmentDto(x.Id, x.OrderId, x.TrackingNumber, x.TrackingUrl, x.ShippedDateUtc, x.DeliveryDateUtc))
            .ToListAsync(ct);
        return new OrderDetailDto(order, items)
        {
            Financials = new(entity.OrderSubtotalInclTax, entity.OrderSubtotalExclTax,
                entity.OrderSubTotalDiscountInclTax, entity.OrderSubTotalDiscountExclTax,
                entity.OrderShippingInclTax, entity.OrderShippingExclTax,
                entity.PaymentMethodAdditionalFeeInclTax, entity.PaymentMethodAdditionalFeeExclTax,
                entity.OrderTax, entity.OrderDiscount, entity.CreditBalance, giftAmount, entity.OrderTotalRounding,
                entity.OrderTotal, entity.RefundedAmount, entity.CustomerCurrencyCode, entity.CurrencyRate),
            BillingAddress = MapAddress(entity.BillingAddress), ShippingAddress = MapAddress(entity.ShippingAddress),
            ShippingMethod = entity.ShippingMethod, PaymentMethodSystemName = entity.PaymentMethodSystemName,
            CustomerComment = entity.CustomerOrderComment, Shipments = shipments
        };
    }

    private static OrderAddressDto MapAddress(Address address) => address == null ? null :
        new(address.Id, address.FirstName, address.LastName, address.Company, address.Country?.Name,
            address.StateProvince?.Name, address.City, address.Address1, address.Address2,
            address.ZipPostalCode, address.PhoneNumber, address.Email, address.CityId, address.Latitude, address.Longitude);

    private async Task<Dictionary<int, OrderProductPreviewDto>> CurrentProductsAsync(IEnumerable<int> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToArray();
        var visible = StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AsNoTracking().Where(x => ids.Contains(x.Id));
        var products = await visible.Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var pictures = await db.Set<ProductMediaFile>().AsNoTracking()
            .Where(x => visible.Any(p => p.Id == x.ProductId) &&
                !x.MediaFile.Deleted && !x.MediaFile.Hidden && !x.MediaFile.IsTransient)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new { x.ProductId, x.MediaFileId }).ToListAsync(ct);
        var pictureIds = pictures.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First().MediaFileId);
        var result = new Dictionary<int, OrderProductPreviewDto>();
        foreach (var product in products)
            result.Add(product.Id, new(product.Id, product.Name,
                pictureIds.TryGetValue(product.Id, out var picture) ? await media.GetUrlAsync(picture, 400, doFallback: false) : null));
        return result;
    }

    public Task<Order> FindOwnedAsync(int id, CancellationToken ct) => Owned().FirstOrDefaultAsync(x => x.Id == id, ct);
}
