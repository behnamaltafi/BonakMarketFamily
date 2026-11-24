using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Common.Paging;
using Common.DTO;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Security;
using BonakMarket.Core.Data;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/orders")]
public sealed class OrdersController(SmartDbContext db, IOrderProcessingService processing, ISettingFactory settings) : StorefrontController
{
    private IQueryable<OrderDto> Orders() => StorefrontScope.Orders(db, Customer, StoreId).AsNoTracking()
        .Select(x => new OrderDto { Id = x.Id, OrderNumber = x.OrderNumber, CreatedOnUtc = x.CreatedOnUtc,
            OrderTotal = x.OrderTotal, OrderStatusId = x.OrderStatusId, PaymentStatusId = x.PaymentStatusId,
            ShippingStatusId = x.ShippingStatusId });

    [HttpPost("search")]
    [ProducesResponseType(typeof(ApiResult<PagingResult<OrderDto>>), 200)]
    public async Task<IActionResult> Search([FromBody] PagingFilter filter)
    {
        if (!StorefrontPaging.Validate<OrderDto>(filter, "Id", "OrderNumber", "CreatedOnUtc", "OrderTotal",
            "OrderStatusId", "PaymentStatusId", "ShippingStatusId")) return Error(400, "invalid_paging");
        return Success(await Orders().FilterPaging(filter));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResult<OrderDetailDto>), 200)]
    public async Task<IActionResult> Get(int id)
    {
        var order = await Orders().FirstOrDefaultAsync(x => x.Id == id);
        if (order == null) return Error(404, "order_not_found");
        var own = StorefrontScope.Orders(db, Customer, StoreId).Where(x => x.Id == id).Select(x => x.Id);
        var lines = await db.Set<OrderItem>().AsNoTracking().Where(x => own.Contains(x.OrderId))
            .Select(x => new OrderLineDto(x.Id, x.ProductId, x.Quantity, x.UnitPriceInclTax, x.PriceInclTax)).ToListAsync();
        return Success(new OrderDetailDto(order, lines));
    }
    [HttpPost("{id:int}/reorder")]
    public async Task<IActionResult> ReOrder(int id)
    {
        if (!await PermissionsService.AuthorizeAsync(Permissions.Cart.AccessShoppingCart, Customer))
            return Error(403, "cart_access_denied");
        var order = await StorefrontScope.Orders(db, Customer, StoreId).FirstOrDefaultAsync(x => x.Id == id);
        if (order == null) return Error(404, "order_not_found");
        if (!(await settings.LoadSettingsAsync<OrderSettings>(StoreId)).IsReOrderAllowed)
            return Error(403, "reorder_not_allowed");
        await processing.ReOrderAsync(order);
        // The native reorder may skip unavailable items. Re-read the cart for the resulting contents.
        return Ok(new ApiResult());
    }
}
