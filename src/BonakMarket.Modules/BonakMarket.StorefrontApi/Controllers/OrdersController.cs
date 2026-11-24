using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/orders")]
[StorefrontPermission(StorefrontPermissions.Orders)]
public sealed class OrdersController(OrderQueries orders, IOrderProcessingService processing, ISettingFactory settings,
    StorefrontRequestContext request, IPaymentService payments) : StorefrontController
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

    [HttpGet("{id:int}/browser-actions")]
    public async Task<ActionResult<OrderBrowserActionsDto>> BrowserActions(int id, CancellationToken ct)
    {
        var order = await orders.FindOwnedAsync(id, ct);
        if (order == null) return Error(404, "order_not_found");
        return Ok(new OrderBrowserActionsDto(Url.Action("Print", "Order", new { id, pdf = true, area = "" }),
            Url.Action("Details", "Order", new { id, area = "" }), await payments.CanRePostProcessPaymentAsync(order), true));
    }

    [HttpPost("{id:int}/reorder")]
    [StorefrontPermission(StorefrontPermissions.Cart)]
    [StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
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
