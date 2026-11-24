using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.OfflinePayment.Models;
using BonakMarket.Web.Components;

namespace BonakMarket.OfflinePayment.Components;

public class PurchaseOrderNumberViewComponent : SmartViewComponent
{
    private readonly ICheckoutStateAccessor _checkoutStateAccessor;

    public PurchaseOrderNumberViewComponent(ICheckoutStateAccessor checkoutStateAccessor)
    {
        _checkoutStateAccessor = checkoutStateAccessor;
    }

    public IViewComponentResult Invoke()
    {
        var paymentData = _checkoutStateAccessor.CheckoutState.PaymentData;
        var model = new PurchaseOrderNumberPaymentInfoModel
        {
            PurchaseOrderNumber = (string)paymentData.Get("PurchaseOrderNumber")
        };

        return View(model);
    }
}