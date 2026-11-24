using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/orders")]
[StorefrontPermission(StorefrontPermissions.Orders)]
public sealed class OrdersController(OrderQueries orders, IOrderProcessingService processing, ISettingFactory settings,
    StorefrontRequestContext request, IPaymentService payments, IPermissionService permissions) : StorefrontController
{
    [HttpGet]
    public async Task<ActionResult<PageDto<OrderDto>>> Get([FromQuery] OrderSearchRequest filter, CancellationToken ct) =>
        Ok(await orders.ListAsync(filter, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDetailDto>> Get(int id, CancellationToken ct)
    {
        var order = await orders.DetailAsync(id, ct);
        if (order == null) return Error(404, "order_not_found");
        var entity = await orders.FindOwnedAsync(id, ct);
        if (entity == null) return Error(404, "order_not_found");
        var canReorder = (await settings.LoadSettingsAsync<OrderSettings>(request.StoreId)).IsReOrderAllowed &&
            await permissions.AuthorizeAsync(StorefrontPermissions.Cart, request.Customer) &&
            await permissions.AuthorizeAsync(Permissions.Cart.AccessShoppingCart, request.Customer);
        var canReturn = processing.CanReturnItems(entity) &&
            await permissions.AuthorizeAsync(StorefrontPermissions.Returns, request.Customer);
        var canRetry = await payments.CanRePostProcessPaymentAsync(entity);
        return Ok(order with
        {
            AllowedActions = new(canReorder, false, canReturn, canRetry, false, true),
            BrowserInvoiceUrl = Url.Action("Print", "Order", new { id, pdf = true, area = "" }),
            BrowserPaymentUrl = canRetry ? Url.Action("Details", "Order", new { id, area = "" }) : null
        });
    }

    /// <summary>Read-only native order/payment status. This endpoint never verifies or captures a gateway payment.</summary>
    [HttpGet("{id:int}/payment-status")]
    public async Task<ActionResult<OrderPaymentStatusDto>> PaymentStatus(int id, CancellationToken ct)
    {
        var order = await orders.FindOwnedAsync(id, ct);
        if (order == null) return Error(404, "order_not_found");
        var canRetry = await payments.CanRePostProcessPaymentAsync(order);
        return Ok(new OrderPaymentStatusDto(order.Id, OrderStatusMapping.Order(order.OrderStatusId),
            OrderStatusMapping.Payment(order.PaymentStatusId), order.PaymentStatus == BonakMarket.Core.Checkout.Payment.PaymentStatus.Paid,
            order.PaidDateUtc, order.UpdatedOnUtc, order.OrderTotal, order.RefundedAmount,
            order.CustomerCurrencyCode, order.CurrencyRate, canRetry, true,
            canRetry ? Url.Action("Details", "Order", new { id, area = "" }) : null));
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
