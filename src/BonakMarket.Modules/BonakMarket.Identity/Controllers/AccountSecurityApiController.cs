using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Services;

namespace BonakMarket.Identity.Controllers;

/// <summary>Authenticated phone enrollment and change; separate from sign-in OTP.</summary>
[ApiController]
[Route("api/identity/v1/me/security/phone")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountSecurityApiController : CustomerAccountApiController
{
    private readonly ISmsOtpLoginService _otp;

    public AccountSecurityApiController(ISmsOtpLoginService otp, SignInManager<Customer> signInManager,
        UserManager<Customer> userManager, IAntiforgery antiforgery)
        : base(signInManager, userManager, antiforgery)
    {
        _otp = otp;
    }

    /// <summary>Requests a phone binding for the currently signed-in account only.</summary>
    [HttpPost("request"), HttpPost("~/api/sms-login/enroll/request"), HttpPost("~/api/sms-login/profile/phone/request"), AllowAnonymous, IgnoreAntiforgeryToken]
    public async Task<IActionResult> RequestEnrollment([FromBody] SmsCodeRequest request, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;

        await _otp.RequestEnrollmentCodeAsync(customer, request.Phone, ct);
        return Ok(new { code = "code_requested" });
    }

    /// <summary>Confirms a phone binding without accepting a client-supplied customer ID.</summary>
    [HttpPost("verify"), HttpPost("~/api/sms-login/enroll/verify"), HttpPost("~/api/sms-login/profile/phone/verify"), AllowAnonymous, IgnoreAntiforgeryToken]
    public async Task<IActionResult> ConfirmEnrollment([FromBody] SmsCodeVerificationRequest request, CancellationToken ct)
    {
        if (!await _otp.IsEnabledAsync())
        {
            return NotFound();
        }

        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;

        if (!await _otp.ConfirmEnrollmentAsync(customer, request.Phone, request.Code, ct))
        {
            return BadRequest(new { code = "invalid_code" });
        }

        return Ok(new { code = "phone_verified" });
    }
}
