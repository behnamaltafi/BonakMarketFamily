using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Security;

namespace BonakMarket.Identity.Controllers;

/// <summary>Same-origin MVC AJAX only. Native application cookie plus antiforgery on writes.</summary>
public abstract class MvcCustomerAccountController(SignInManager<Customer> signInManager, UserManager<Customer> userManager) : ControllerBase
{
    protected readonly SignInManager<Customer> _signInManager = signInManager;

    protected async Task<(Customer Customer, IActionResult Error)> RequireAccountAsync()
    {
        if (!IdentityTransportPolicy.IsAllowed(Request))
            return (null, BadRequest(new IdentityCodeResponse("https_required")));
        var auth = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var customer = auth.Succeeded && auth.Principal != null && signInManager.IsSignedIn(auth.Principal)
            ? await userManager.GetUserAsync(auth.Principal) : null;
        var work = HttpContext.RequestServices.GetRequiredService<IWorkContext>();
        if (customer == null || !customer.Active || customer.Deleted || customer.IsSystemAccount ||
            !customer.IsRegistered() || work.CurrentImpersonator != null || work.CurrentCustomer?.Id != customer.Id)
            return (null, Unauthorized());
        return (customer, null);
    }
}
