using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Identity;
using Smartstore.Core.Data;
using Smartstore.MultiSms.Models;
using Smartstore.MultiSms.Services;
using Smartstore.Web.Controllers;

namespace Smartstore.MultiSms.Controllers;

/// <summary>
/// Passwordless login and authenticated phone verification for the original
/// Smartstore Customer identity. No AppUser model is required.
/// </summary>
[Route("sms-login")]
public sealed class SmsLoginController : PublicController
{
    private readonly SmsOtpLoginService _otp;
    private readonly SignInManager<Customer> _signInManager;
    private readonly UserManager<Customer> _userManager;
    private readonly SmartDbContext _db;

    public SmsLoginController(SmsOtpLoginService otp,
        SignInManager<Customer> signInManager,
        UserManager<Customer> userManager,
        SmartDbContext db)
    {
        _otp = otp;
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string returnUrl = null)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        return View(new SmsOtpLoginModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    [HttpPost("request")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCode(SmsOtpLoginModel model, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        if (SmsPhoneNormalizer.Normalize(model.Phone) is not null)
        {
            await _otp.RequestLoginCodeAsync(model.Phone, ct);
        }

        // The response is independent of whether an account is registered.
        ModelState.Clear();
        model.CodeRequested = true;
        model.StatusMessage = "If this number has been verified, a code will be sent.";
        return View("Index", model);
    }

    [HttpPost("verify")]
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
            await _userManager.IsLockedOutAsync(customer) ||
            await _userManager.GetTwoFactorEnabledAsync(customer))
        {
            ModelState.AddModelError(nameof(model.Code), "Invalid or expired code.");
            model.CodeRequested = true;
            return View("Index", model);
        }

        await _signInManager.SignInAsync(customer, isPersistent: false);
        customer.LastLoginDateUtc = DateTime.UtcNow;
        // The OTP service returns a tracked Customer entity, so the standard
        // SmartDbContext unit of work saves the last-login update here.
        await _db.SaveChangesAsync(ct);

        return model.ReturnUrl is not null
            ? LocalRedirect(model.ReturnUrl)
            : RedirectToRoute("Homepage");
    }

    [HttpGet("enroll")]
    public async Task<IActionResult> Enroll()
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        var customer = Services.WorkContext.CurrentCustomer;
        if (customer is null || !customer.Active || customer.Deleted ||
            !customer.IsRegistered() || !_signInManager.IsSignedIn(User))
        {
            return RedirectToRoute("Login");
        }

        return View(new SmsOtpLoginModel
        {
            IsEnrolling = true,
            VerifiedPhone = await _otp.GetVerifiedPhoneAsync(customer.Id)
        });
    }

    [HttpPost("enroll/request")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestEnrollment(SmsOtpLoginModel model, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        var customer = Services.WorkContext.CurrentCustomer;
        if (customer is null || !customer.Active || customer.Deleted ||
            !customer.IsRegistered() || !_signInManager.IsSignedIn(User))
        {
            return RedirectToRoute("Login");
        }

        await _otp.RequestEnrollmentCodeAsync(customer, model.Phone, ct);
        ModelState.Clear();
        model.IsEnrolling = true;
        model.CodeRequested = true;
        model.VerifiedPhone = await _otp.GetVerifiedPhoneAsync(customer.Id, ct);
        model.StatusMessage = "If the number is eligible, a verification code will be sent.";
        return View("Enroll", model);
    }

    [HttpPost("enroll/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmEnrollment(SmsOtpLoginModel model, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        var customer = Services.WorkContext.CurrentCustomer;
        if (customer is null || !customer.Active || customer.Deleted ||
            !customer.IsRegistered() || !_signInManager.IsSignedIn(User))
        {
            return RedirectToRoute("Login");
        }

        if (!await _otp.ConfirmEnrollmentAsync(customer, model.Phone, model.Code, ct))
        {
            ModelState.AddModelError(nameof(model.Code), "Invalid or expired code.");
            model.IsEnrolling = true;
            model.CodeRequested = true;
            model.VerifiedPhone = await _otp.GetVerifiedPhoneAsync(customer.Id, ct);
            return View("Enroll", model);
        }

        return RedirectToAction(nameof(Enroll));
    }
}
