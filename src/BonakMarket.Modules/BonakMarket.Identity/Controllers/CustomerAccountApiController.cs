using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using BonakMarket.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Identity;

namespace BonakMarket.Identity.Controllers;

/// <summary>Shared self-service authentication and cookie CSRF boundary.</summary>
public abstract class CustomerAccountApiController : ControllerBase
{
    protected readonly SignInManager<Customer> _signInManager;
    protected readonly UserManager<Customer> _userManager;
    protected readonly IAntiforgery _antiforgery;

    protected CustomerAccountApiController(SignInManager<Customer> signInManager,
        UserManager<Customer> userManager, IAntiforgery antiforgery)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _antiforgery = antiforgery;
    }

    protected async Task<(Customer Customer, IActionResult Error)> RequireAccountAsync()
    {
        if (!Request.IsHttps) return (null, BadRequest(new { code = "https_required" }));
        var customer = await GetCustomerAsync();
        if (customer is null) return (null, Unauthorized());
        if (!Request.Headers.ContainsKey("Authorization") && !HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method))
        {
            try { await _antiforgery.ValidateRequestAsync(HttpContext); }
            catch (AntiforgeryValidationException) { return (null, BadRequest(new { code = "invalid_antiforgery_token" })); }
        }
        return (customer, null);
    }

    protected async Task<Customer> GetCustomerAsync()
    {
        Customer customer;
        if (Request.Headers.ContainsKey("Authorization"))
        {
            customer = HttpContext.Items.TryGetValue(typeof(IMobileApiTokenService), out var verified)
                ? verified as Customer : null;
        }
        else
        {
            if (!_signInManager.IsSignedIn(User)) return null;
            customer = await _userManager.GetUserAsync(User);
        }

        if (customer is null || !customer.Active || customer.Deleted || !customer.IsRegistered() || customer.IsSystemAccount)
            return null;

        var work = HttpContext.RequestServices.GetRequiredService<IWorkContext>();
        return work.CurrentImpersonator == null && work.CurrentCustomer?.Id == customer.Id ? customer : null;
    }
}
