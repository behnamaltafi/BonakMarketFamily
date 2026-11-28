using BonakMarket.Core.Configuration;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;


public sealed class OrderStorefrontService(OrderQueries orders, IOrderProcessingService processing, ISettingFactory settings,
    StorefrontRequestContext request, IPaymentService payments, IPermissionService permissions)
{

    public Task<PageDto<OrderDto>> ListAsync(OrderSearchRequest filter, CancellationToken ct) =>
        orders.ListAsync(filter, ct);

    public async Task<StorefrontResult<OrderDetailDto>> GetAsync(int id, CancellationToken ct)
    {
        var order = await orders.DetailAsync(id, ct);
        if (order == null) return new StorefrontFailure(404, "order_not_found");
        var entity = await orders.FindOwnedAsync(id, ct);
        if (entity == null) return new StorefrontFailure(404, "order_not_found");
        var canReorder = (await settings.LoadSettingsAsync<OrderSettings>(request.StoreId)).IsReOrderAllowed &&
            await permissions.AuthorizeAsync(StorefrontPermissions.Cart, request.Customer) &&
            await permissions.AuthorizeAsync(Permissions.Cart.AccessShoppingCart, request.Customer);
        var canReturn = processing.CanReturnItems(entity) &&
            await permissions.AuthorizeAsync(StorefrontPermissions.Returns, request.Customer);
        var canRetry = await payments.CanRePostProcessPaymentAsync(entity);
        return new(order with
        {
            AllowedActions = new(canReorder, false, canReturn, canRetry, false, true)
        });
    }

    /// <summary>Read-only native order/payment status. This endpoint never verifies or captures a gateway payment.</summary>

    public async Task<StorefrontResult<OrderPaymentStatusDto>> PaymentStatusAsync(int id, CancellationToken ct)
    {
        var order = await orders.FindOwnedAsync(id, ct);
        if (order == null) return new StorefrontFailure(404, "order_not_found");
        var canRetry = await payments.CanRePostProcessPaymentAsync(order);
        return new(new OrderPaymentStatusDto(order.Id, OrderStatusMapping.Order(order.OrderStatusId),
            OrderStatusMapping.Payment(order.PaymentStatusId), order.PaymentStatus == BonakMarket.Core.Checkout.Payment.PaymentStatus.Paid,
            order.PaidDateUtc, order.UpdatedOnUtc, order.OrderTotal, order.RefundedAmount,
            order.CustomerCurrencyCode, order.CurrencyRate, canRetry, true,
            null));
    }

    public async Task<StorefrontResult<bool>> BrowserActionsAsync(int id, CancellationToken ct)
    {
        var order = await orders.FindOwnedAsync(id, ct);
        if (order == null) return new StorefrontFailure(404, "order_not_found");
        return new(await payments.CanRePostProcessPaymentAsync(order));
    }






    public async Task<StorefrontResult<bool>> ReOrderAsync(int id, CancellationToken ct)
    {
        var order = await orders.FindOwnedAsync(id, ct);
        if (order == null) return new StorefrontFailure(404, "order_not_found");
        if (!(await settings.LoadSettingsAsync<OrderSettings>(request.StoreId)).IsReOrderAllowed)
            return new StorefrontFailure(403, "reorder_not_allowed");
        await processing.ReOrderAsync(order);
        return new(true);
    }
}
