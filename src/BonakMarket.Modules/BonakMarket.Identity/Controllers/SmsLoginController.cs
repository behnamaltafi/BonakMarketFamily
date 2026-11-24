using BonakMarket.Core.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Services;
using BonakMarket.Web.Controllers;

namespace BonakMarket.Identity.Controllers;

/// <summary>
/// Passwordless login and authenticated phone verification for the original
/// BonakMarket Customer identity. No AppUser model is required.
/// </summary>
[Route("sms-login")]
public sealed class SmsLoginController : PublicController
{
    private readonly SmsOtpLoginService _otp;
    private readonly IStorefrontSmsProfileService _profile;
    private readonly SignInManager<Customer> _signInManager;
    private readonly UserManager<Customer> _userManager;
    private readonly SmartDbContext _db;
    private readonly IShoppingCartService _shoppingCartService;

    public SmsLoginController(SmsOtpLoginService otp, IStorefrontSmsProfileService profile,
        SignInManager<Customer> signInManager,
        UserManager<Customer> userManager,
        SmartDbContext db,
        IShoppingCartService shoppingCartService)
    {
        _otp = otp;
        _profile = profile;
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _shoppingCartService = shoppingCartService;
    }

    [HttpGet("")]
    [AllowAnonymous, NeverAuthorize, CheckStoreClosed(false)]
    public async Task<IActionResult> Index(string returnUrl = null, bool checkoutAsGuest = false)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        return View(new SmsOtpLoginModel
        {
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            CheckoutAsGuest = checkoutAsGuest
        });
    }

    [HttpPost("request")]
    [AllowAnonymous, NeverAuthorize, CheckStoreClosed(false)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCode(SmsOtpLoginModel model, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        if (SmsPhoneNormalizer.Normalize(model.Phone) is null)
        {
            ModelState.AddModelError(nameof(model.Phone), T("Plugins.Identity.Customer.Otp.InvalidPhone").Value);
            return View("Index", model);
        }

        await _otp.RequestLoginCodeAsync(model.Phone, ct);

        // The response is independent of whether an account is registered.
        ModelState.Clear();
        model.CodeRequested = true;
        model.StatusMessage = T("Plugins.Identity.Customer.Otp.CodeSentGeneric").Value;
        return View("Index", model);
    }

    [HttpPost("verify")]
    [AllowAnonymous, NeverAuthorize, CheckStoreClosed(false)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyCode(SmsOtpLoginModel model, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        var customer = await _otp.ConfirmLoginAsync(model.Phone, model.Code, ct);

        if (customer is null || customer.Deleted || !customer.Active ||
            !customer.IsRegistered() ||
            !await _signInManager.CanSignInAsync(customer) ||
            (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(customer)) ||
            (_userManager.SupportsUserTwoFactor && await _userManager.GetTwoFactorEnabledAsync(customer)))
        {
            ModelState.AddModelError(nameof(model.Code), T("Plugins.Identity.Customer.Otp.InvalidCode").Value);
            model.CodeRequested = true;
            return View("Index", model);
        }

        var guest = Services.WorkContext.CurrentCustomer;
        await _signInManager.SignInAsync(customer, isPersistent: false);
        await _shoppingCartService.MigrateCartAsync(guest, customer);
        await Services.EventPublisher.PublishAsync(new CustomerSignedInEvent { Customer = customer });
        customer.LastLoginDateUtc = DateTime.UtcNow;
        // The OTP service returns a tracked Customer entity, so the standard
        // SmartDbContext unit of work saves the last-login update here.
        await _db.SaveChangesAsync(ct);

        if (await _otp.IsProfileIncompleteAsync(customer.Id, ct))
        {
            return RedirectToAction(nameof(Profile), new { returnUrl = model.ReturnUrl });
        }

        return model.ReturnUrl is not null
            ? LocalRedirect(model.ReturnUrl)
            : RedirectToRoute("Homepage");
    }

    /// <summary>
    /// A verified new phone account reaches this page immediately after first login.
    /// It is also available later so username can change without changing the login phone.
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(string returnUrl = null, CancellationToken ct = default)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        var customer = await GetSignedInCustomerAsync();
        if (customer is null || await _otp.GetVerifiedPhoneAsync(customer.Id, ct) is null)
        {
            return RedirectToRoute("Login");
        }

        return View(new SmsProfileModel
        {
            Phone = await _otp.GetVerifiedPhoneAsync(customer.Id, ct),
            Username = customer.Username,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email?.EndsWith("@accounts.invalid", StringComparison.OrdinalIgnoreCase) == true
                ? null : customer.Email,
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProfile(SmsProfileModel model, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        var customer = await GetSignedInCustomerAsync();
        if (customer is null || await _otp.GetVerifiedPhoneAsync(customer.Id, ct) is null)
        {
            return RedirectToRoute("Login");
        }

        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        model.Phone = await _otp.GetVerifiedPhoneAsync(customer.Id, ct);
        if (!ModelState.IsValid)
        {
            return View("Profile", model);
        }

        var result = await _profile.CompleteProfileAsync(
            customer, model.Username, model.FirstName, model.LastName, model.Email, ct);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View("Profile", model);
        }

        return model.ReturnUrl is not null ? LocalRedirect(model.ReturnUrl) : RedirectToRoute("Homepage");
    }

    private async Task<Customer> GetSignedInCustomerAsync()
    {
        if (!_signInManager.IsSignedIn(User))
        {
            return null;
        }

        var customer = await _userManager.GetUserAsync(User);
        return customer is not null && customer.Active && !customer.Deleted &&
            customer.IsRegistered() ? customer : null;
    }

    /// <summary>
    /// Compatibility redirect: binding or changing a mobile now happens inline
    /// on BonakMarket's native Customer profile instead of a separate page.
    /// </summary>
    [HttpGet("enroll")]
    public async Task<IActionResult> Enroll()
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        if (await GetSignedInCustomerAsync() is null)
        {
            return RedirectToRoute("Login", new
            {
                method = "password",
                returnUrl = Url.Action("Info", "Customer")
            });
        }

        return RedirectToAction("Info", "Customer");
    }
}
