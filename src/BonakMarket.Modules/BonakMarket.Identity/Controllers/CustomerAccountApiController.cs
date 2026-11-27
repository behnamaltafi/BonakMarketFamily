using Microsoft.AspNetCore.Http;
using BonakMarket.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Security;

namespace BonakMarket.Identity.Controllers;

/// <summary>JWT-only current-customer boundary; MVC cookie consumers use dedicated controllers.</summary>
public abstract class CustomerAccountApiController : ControllerBase
{
    protected Task<(Customer Customer, IActionResult Error)> RequireAccountAsync()
    {
        if (!IdentityTransportPolicy.IsAllowed(Request))
            return Task.FromResult<(Customer, IActionResult)>((null, BadRequest(new IdentityCodeResponse("https_required"))));
        var customer = GetCustomer();
        if (customer == null)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return Task.FromResult<(Customer, IActionResult)>((null, Unauthorized()));
        }
        return Task.FromResult<(Customer, IActionResult)>((customer, null));
    }

    private Customer GetCustomer()
    {
        var customer = HttpContext.Items.TryGetValue(typeof(IMobileApiTokenService), out var verified)
            ? verified as Customer : null;
        if (customer == null || !customer.Active || customer.Deleted || !customer.IsRegistered() || customer.IsSystemAccount)
            return null;

        var work = HttpContext.RequestServices.GetRequiredService<IWorkContext>();
        return work.CurrentImpersonator == null && work.CurrentCustomer?.Id == customer.Id ? customer : null;
    }
}
