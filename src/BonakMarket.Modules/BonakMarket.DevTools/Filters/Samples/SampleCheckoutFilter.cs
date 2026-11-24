using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using BonakMarket.Core.Logging;

namespace BonakMarket.DevTools.Filters.Samples;

public class SampleCheckoutFilter : IActionFilter
{
    private readonly INotifier _notifier;

    public SampleCheckoutFilter(INotifier notifier)
    {
        _notifier = notifier;
    }

    public void OnActionExecuting(ActionExecutingContext filterContext)
    {
    }

    public void OnActionExecuted(ActionExecutedContext filterContext)
    {
        filterContext.Result = new RedirectToRouteResult(new { action = "MyBillingAddress", controller = "MyCheckout" });
    }
}