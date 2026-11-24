using Amazon.Pay.API.WebStore.ChargePermission;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Checkout.Orders.Events;
using BonakMarket.Events;

namespace BonakMarket.AmazonPay;

public class Events : IConsumer
{
    public Localizer T { get; set; } = NullLocalizer.Instance;

    public void HandleEvent(OrderPaidEvent message,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger)
    {
        var order = message.Order;
        var httpContext = httpContextAccessor?.HttpContext;

        if (order != null
            && httpContext != null
            && order.PaymentMethodSystemName.EqualsNoCase(AmazonPayProvider.SystemName)
            && order.AuthorizationTransactionCode.HasValue())
        {
            try
            {
                var client = httpContext.GetAmazonPayApiClient(order.StoreId);
                var request = new CloseChargePermissionRequest(T("Plugins.Payments.AmazonPay.CloseChargeReason").Value.Truncate(255))
                {
                    CancelPendingCharges = false
                };

                client.CloseChargePermission(order.AuthorizationTransactionCode, request);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }
    }
}