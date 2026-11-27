using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Services;

namespace BonakMarket.Identity.Controllers;

/// <summary>Authenticated phone enrollment and change; separate from sign-in OTP.</summary>
[ApiController]
[ApiExplorerSettings(GroupName = "identity")]
[Route("api/identity/v1/me/security/phone")]
[IgnoreAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountSecurityApiController : CustomerAccountApiController
{
    private readonly ISmsOtpLoginService _otp;

    public AccountSecurityApiController(ISmsOtpLoginService otp)
    {
        _otp = otp;
    }

    [HttpPost("reauth/request"), AllowAnonymous, IgnoreAntiforgeryToken]
    public async Task<IActionResult> RequestReauth(CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error != null) return error;
        if (!await _otp.IsReauthenticationEnabledAsync()) return NotFound();
        return await _otp.RequestReauthAsync(customer, ct) ? Ok(new IdentityCodeResponse("code_requested")) : BadRequest(new IdentityCodeResponse("reauth_unavailable"));
    }

    [HttpPost("reauth/verify"), AllowAnonymous, IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(PhoneReauthResponse), 200)]
    public async Task<IActionResult> VerifyReauth([FromBody] PhoneReauthRequest request, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error != null) return error;
        if (!await _otp.IsVerificationEnabledAsync()) return NotFound();
        var proof = await _otp.ConfirmReauthAsync(customer, request.Code, ct);
        return proof == null ? BadRequest(new IdentityCodeResponse("invalid_code")) : Ok(new PhoneReauthResponse(proof, 600));
    }

    /// <summary>Requests a phone binding for the currently signed-in account only.</summary>
    [HttpPost("request"), AllowAnonymous, IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(IdentityCodeResponse), 200)]
    public async Task<IActionResult> RequestEnrollment([FromBody] PhoneChangeRequest request, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;

        if (!await _otp.IsEnrollmentEnabledAsync())
        {
            return NotFound();
        }

        var old = await _otp.GetVerifiedPhoneAsync(customer.Id, ct);
        if (old != null)
        {
            if (!await _otp.RequestPhoneChangeAsync(customer, request.Phone, request.ReauthToken, ct))
                return BadRequest(new IdentityCodeResponse("reauth_required_or_phone_unavailable"));
        }
        else
        {
            await _otp.RequestEnrollmentCodeAsync(customer, request.Phone, ct);
        }
        return Ok(new IdentityCodeResponse("code_requested"));
    }

    /// <summary>Confirms a phone binding without accepting a client-supplied customer ID.</summary>
    [HttpPost("verify"), AllowAnonymous, IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(PhoneChangeResponse), 200)]
    public async Task<IActionResult> ConfirmEnrollment([FromBody] PhoneChangeVerificationRequest request, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;

        if (!await _otp.IsVerificationEnabledAsync())
        {
            return NotFound();
        }

        var old = await _otp.GetVerifiedPhoneAsync(customer.Id, ct);
        var success = old == null
            ? await _otp.ConfirmEnrollmentAsync(customer, request.Phone, request.Code, ct)
            : await _otp.ConfirmPhoneChangeAsync(customer, request.Phone, request.Code, request.ReauthToken, ct);
        if (!success)
        {
            return BadRequest(new IdentityCodeResponse("invalid_code"));
        }

        // Phone change revokes all customer API sessions in the OTP service.
        return Ok(new PhoneChangeResponse("phone_verified", true));
    }
}
