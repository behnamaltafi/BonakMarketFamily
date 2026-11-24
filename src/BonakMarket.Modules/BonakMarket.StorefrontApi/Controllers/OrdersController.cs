using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/orders")]
[StorefrontPermission(StorefrontPermissions.Orders)]
public sealed class OrdersController(OrderQueries orders, IOrderProcessingService processing, ISettingFactory settings,
    StorefrontRequestContext request) : StorefrontController
{
    [HttpGet]
    public async Task<ActionResult<PageDto<OrderDto>>> Get([FromQuery] OrderSearchRequest filter, CancellationToken ct) =>
        Ok(await orders.ListAsync(filter, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDetailDto>> Get(int id, CancellationToken ct)
    {
        var order = await orders.DetailAsync(id, ct);
        if (order == null) return Error(404, "order_not_found");
        return Ok(order);
    }

    [HttpPost("{id:int}/reorder")]
    [StorefrontPermission(StorefrontPermissions.Cart)]
    [StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
    public async Task<IActionResult> ReOrder(int id, CancellationToken ct)
    {
        var order = await orders.FindOwnedAsync(id, ct);
        if (order == null) return Error(404, "order_not_found");
        if (!(await settings.LoadSettingsAsync<OrderSettings>(request.StoreId)).IsReOrderAllowed)
            return Error(403, "reorder_not_allowed");
        await processing.ReOrderAsync(order);
        return NoContent();
    }
}
