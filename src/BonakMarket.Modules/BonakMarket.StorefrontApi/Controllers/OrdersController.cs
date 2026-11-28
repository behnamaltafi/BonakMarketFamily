using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/orders")]
[StorefrontPermission(StorefrontPermissions.Orders)]
public sealed class OrdersController(OrderStorefrontService orders) : StorefrontController
{
    [HttpGet]
    public async Task<ActionResult<PageDto<OrderDto>>> Get([FromQuery] OrderSearchRequest filter, CancellationToken ct) =>
        Ok(await orders.ListAsync(filter, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDetailDto>> Get(int id, CancellationToken ct)
    {
        var result = await orders.GetAsync(id, ct);
        if (result.Failure != null) return FailureResponse(result.Failure);
        var order = result.Value;
        return Ok(order with
        {
            BrowserInvoiceUrl = Url.Action("Print", "Order", new { id, pdf = true, area = "" }),
            BrowserPaymentUrl = order.AllowedActions.CanRetryPayment ? Url.Action("Details", "Order", new { id, area = "" }) : null
        });
    }

    /// <summary>Read-only native order/payment status. This endpoint never verifies or captures a gateway payment.</summary>
    [HttpGet("{id:int}/payment-status")]
    public async Task<ActionResult<OrderPaymentStatusDto>> PaymentStatus(int id, CancellationToken ct)
    {
        var result = await orders.PaymentStatusAsync(id, ct);
        if (result.Failure != null) return FailureResponse(result.Failure);
        var order = result.Value;
        return Ok(order with
        {
            BrowserPaymentUrl = order.CanRetryPayment ? Url.Action("Details", "Order", new { id, area = "" }) : null
        });
    }

    [HttpGet("{id:int}/browser-actions")]
    public async Task<ActionResult<OrderBrowserActionsDto>> BrowserActions(int id, CancellationToken ct)
    {
        var result = await orders.BrowserActionsAsync(id, ct);
        if (result.Failure != null) return FailureResponse(result.Failure);
        return Ok(new OrderBrowserActionsDto(Url.Action("Print", "Order", new { id, pdf = true, area = "" }),
            Url.Action("Details", "Order", new { id, area = "" }), result.Value, true));
    }

    [HttpPost("{id:int}/reorder")]
    [StorefrontPermission(StorefrontPermissions.Cart)]
    [StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 403)]
    public async Task<IActionResult> ReOrder(int id, CancellationToken ct) =>
        RespondNoContent(await orders.ReOrderAsync(id, ct));
}
