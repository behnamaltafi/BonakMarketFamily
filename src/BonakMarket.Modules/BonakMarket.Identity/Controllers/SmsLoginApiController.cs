using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Events;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Services;

namespace BonakMarket.Identity.Controllers;

/// <summary>Passwordless SMS login and registration only.</summary>
[ApiController]
[Route("api/identity/v1/auth/sms")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SmsLoginApiController : ControllerBase
{
    private readonly ISmsOtpLoginService _otp;
    private readonly SignInManager<Customer> _signInManager;
    private readonly UserManager<Customer> _userManager;
    private readonly SmartDbContext _db;
    private readonly IEventPublisher _events;

    public SmsLoginApiController(ISmsOtpLoginService otp, SignInManager<Customer> signInManager,
        UserManager<Customer> userManager, SmartDbContext db, IEventPublisher events)
    {
        _otp = otp;
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
        _events = events;
    }

    /// <summary>Requests a login code with an account-independent response.</summary>
    [HttpPost("request"), HttpPost("~/api/sms-login/request"), AllowAnonymous]
    public async Task<IActionResult> RequestCode([FromBody] SmsCodeRequest request, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        await _otp.RequestLoginCodeAsync(request.Phone, ct);
        return Ok(new { code = "code_requested" });
    }

    /// <summary>Consumes an OTP and creates the native, non-persistent login cookie.</summary>
    [HttpPost("verify"), HttpPost("~/api/sms-login/verify"), AllowAnonymous]
    public async Task<IActionResult> VerifyCode([FromBody] SmsCodeVerificationRequest request, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        var tokenService = request.IssueTokens ? HttpContext.RequestServices.GetService<IMobileApiTokenService>() : null;
        if (request.IssueTokens && (!Request.IsHttps || tokenService?.IsEnabled != true))
            return StatusCode(503, new { code = "mobile_tokens_unavailable" });

        var customer = await _otp.ConfirmLoginAsync(request.Phone, request.Code, ct);
        if (customer is null || customer.Deleted || !customer.Active || !customer.IsRegistered() ||
            !await _signInManager.CanSignInAsync(customer) ||
            (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(customer)) ||
            (_userManager.SupportsUserTwoFactor && await _userManager.GetTwoFactorEnabledAsync(customer)))
        {
            return BadRequest(new { code = "invalid_code" });
        }

        MobileApiTokenPair pair = null;
        if (request.IssueTokens)
        {
            pair = await tokenService.IssueAsync(customer, ct);
            if (pair == null)
                return Unauthorized(new { code = "mobile_tokens_unavailable" });
        }

        await _signInManager.SignInAsync(customer, isPersistent: false);
        await _events.PublishAsync(new CustomerSignedInEvent { Customer = customer });
        customer.LastLoginDateUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        bool profileRequired = await _otp.IsProfileIncompleteAsync(customer.Id, ct);
        if (request.IssueTokens)
        {
            return Ok(new { code = "signed_in", profileRequired, profilePath = "/sms-login/profile",
                accessToken = pair.AccessToken, refreshToken = pair.RefreshToken,
                expiresIn = pair.ExpiresIn, tokenType = "Bearer" });
        }
        return Ok(new { code = "signed_in", profileRequired, profilePath = "/sms-login/profile" });
    }
}
