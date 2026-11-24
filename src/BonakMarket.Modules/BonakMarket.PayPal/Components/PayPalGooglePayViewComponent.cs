using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.PayPal.Services;

namespace BonakMarket.PayPal.Components;

/// <summary>
/// Renders PayPal button for Google Pay.
/// </summary>
public class PayPalGooglePayViewComponent : PayPalViewComponentBase
{
    protected override IViewComponentResult InvokeCore()
    {
        if (HttpContext.Connection.IsLocal())
        {
            return Empty();
        }

        // Get displayable options from settings depending on location (OffCanvasCart or Cart).
        if (PayPalHelper.IsCartRoute(RouteIdent) && !Settings.FundingsCart.Contains(FundingOptions.googlepay.ToString()))
        {
            return Empty();
        }

        var model = new GooglePayModel
        {
            IsSandbox = Settings.UseSandbox,
            RouteIdent = RouteIdent
        };

        return View(model);
    }
}