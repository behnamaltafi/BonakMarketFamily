using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using BonakMarket.AmazonPay.Components;
using BonakMarket.Core;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Logging;
using BonakMarket.Core.Stores;
using BonakMarket.Core.Widgets;
using BonakMarket.Web.Controllers;

namespace BonakMarket.AmazonPay.Filters;

public class CheckoutFilter : IAsyncActionFilter
{
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;
    private readonly INotifier _notifier;
    private readonly Lazy<IPaymentService> _paymentService;
    private readonly Lazy<IWidgetProvider> _widgetProvider;
    private readonly ICheckoutStateAccessor _checkoutStateAccessor;
    private readonly AmazonPaySettings _settings;
    private readonly OrderSettings _orderSettings;
    private readonly Localizer T;

    public CheckoutFilter(
        IStoreContext storeContext,
        IWorkContext workContext,
        INotifier notifier,
        Lazy<IPaymentService> paymentService,
        Lazy<IWidgetProvider> widgetProvider,
        ICheckoutStateAccessor checkoutStateAccessor,
        AmazonPaySettings settings,
        OrderSettings orderSettings,
        Localizer localizer)
    {
        _storeContext = storeContext;
        _workContext = workContext;
        _notifier = notifier;
        _paymentService = paymentService;
        _widgetProvider = widgetProvider;
        _checkoutStateAccessor = checkoutStateAccessor;
        _settings = settings;
        _orderSettings = orderSettings;
        T = localizer;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var action = context.RouteData.Values.GetActionName();

        if (action.EqualsNoCase(nameof(CheckoutController.PaymentMethod)))
        {
            var state = _checkoutStateAccessor.CheckoutState.GetCustomState<AmazonPayCheckoutState>();
            if (_settings.ShowInPaymentMethodList && state.SessionId.IsEmpty() && await IsAmazonPayActive())
            {
                _widgetProvider.Value.RegisterViewComponent<PayButtonViewComponent>("checkout_payment_method_buttons", new { isPaymentSelection = true });
            }
        }        
        else if (action.EqualsNoCase(nameof(CheckoutController.Completed)))
        {
            var session = context.HttpContext.Session;
            var responseStatus = session.GetString("AmazonPayResponseStatus");
            if (responseStatus.HasValue())
            {
                if (await IsAmazonPayActive())
                {
                    // 202 (Accepted): authorization is pending.
                    string completedNote = responseStatus == "202"
                        ? T("Plugins.Payments.AmazonPay.AsyncPaymentAuthorizationNote")
                        : string.Empty;

                    if (!_orderSettings.DisableOrderCompletedPage)
                    {
                        _widgetProvider.Value.RegisterWidget("checkout_completed_top",
                            new PartialViewWidget("_CheckoutCompleted", completedNote, "BonakMarket.AmazonPay"));
                    }
                    else if (completedNote.HasValue())
                    {
                        _notifier.Information(completedNote);
                    }
                }

                session.TryRemove("AmazonPayResponseStatus");
            }

            await next();
            return;
        }

        if (IsAmazonPaySelected() && await IsAmazonPayActive())
        {
            var state = _checkoutStateAccessor.CheckoutState.GetCustomState<AmazonPayCheckoutState>();
            if (state.Flow == AmazonPayCheckoutFlow.Express && state.SessionId.HasValue())
            {
                _widgetProvider.Value.RegisterWidget("end",
                    new PartialViewWidget("_CheckoutNavigation", state, "BonakMarket.AmazonPay"));
            }
        }

        await next();
    }

    private bool IsAmazonPaySelected()
        => _workContext.CurrentCustomer.GenericAttributes.SelectedPaymentMethod.EqualsNoCase(AmazonPayProvider.SystemName);

    private Task<bool> IsAmazonPayActive()
        => _paymentService.Value.IsPaymentProviderActiveAsync(AmazonPayProvider.SystemName, null, _storeContext.CurrentStore.Id);
}