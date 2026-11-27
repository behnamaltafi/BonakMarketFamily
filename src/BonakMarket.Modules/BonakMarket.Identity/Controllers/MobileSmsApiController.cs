using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Messaging;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Security;
using BonakMarket.Identity.Services;

namespace BonakMarket.Identity.Controllers;

/// <summary>Token-only customer OTP login. Never creates a browser authentication cookie.</summary>
[ApiController, AllowAnonymous, IgnoreAntiforgeryToken]
[ApiExplorerSettings(GroupName = "identity")]
[Route("api/identity/v1/auth/mobile/sms")]
[Consumes("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MobileSmsApiController(SmsOtpLoginService otp, IMobileApiTokenService tokens, SmartDbContext db) : ControllerBase
{
    /// <summary>Requests a challenge without disclosing account existence or delivery outcome.</summary>
    [HttpPost("request")]
    [ProducesResponseType(typeof(MobileSmsChallengeResponse), 200)]
    [ProducesResponseType(typeof(OtpErrorResponse), 429)]
    [ProducesResponseType(typeof(OtpErrorResponse), 400)]
    public async Task<IActionResult> RequestCode([FromBody] SmsCodeRequest input, CancellationToken ct)
    {
        if (!IdentityTransportPolicy.IsAllowed(Request)) return BadRequest(new IdentityCodeResponse("https_required"));
        if (!tokens.IsEnabled) return StatusCode(503, new IdentityCodeResponse("mobile_tokens_unavailable"));
        if (!await otp.IsEnabledAsync()) return NotFound(new IdentityCodeResponse("sms_login_disabled"));
        var phone = SmsPhoneNormalizer.Normalize(input.Phone);
        if (phone == null) return BadRequest(new IdentityCodeResponse("invalid_phone"));
        string challenge;
        try { challenge = await otp.RequestMobileChallengeAsync(phone, ct); }
        catch (OtpFlowException ex)
        {
            if (ex.RetryAfterSeconds.HasValue)
                Response.Headers["Retry-After"] = ex.RetryAfterSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(ex.StatusCode, new OtpErrorResponse(ex.Code, ex.RetryAfterSeconds));
        }
        var policy = await otp.GetOtpSettingsAsync();
        var local = SmsPhoneNormalizer.ToProviderPhone(phone);
        return Ok(new MobileSmsChallengeResponse(challenge, policy.EffectiveOtpExpirationSeconds(),
            policy.EffectiveOtpResendIntervalSeconds(), local[..4] + "***" + local[^4..]));
    }

    /// <summary>Consumes a valid OTP and returns only customer API credentials.</summary>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(MobileSmsSignInResponse), 200)]
    [ProducesResponseType(typeof(OtpErrorResponse), 429)]
    [ProducesResponseType(typeof(OtpErrorResponse), 400)]
    public async Task<IActionResult> VerifyCode([FromBody] MobileSmsVerifyRequest input, CancellationToken ct)
    {
        if (!IdentityTransportPolicy.IsAllowed(Request)) return BadRequest(new IdentityCodeResponse("https_required"));
        if (!tokens.IsEnabled) return StatusCode(503, new IdentityCodeResponse("mobile_tokens_unavailable"));
        if (!await otp.IsVerificationEnabledAsync()) return NotFound(new IdentityCodeResponse("sms_login_disabled"));
        Customer customer;
        try { customer = await otp.ConfirmMobileLoginAsync(input.ChallengeId, input.Code, ct); }
        catch (OtpFlowException ex)
        {
            if (ex.RetryAfterSeconds.HasValue)
                Response.Headers["Retry-After"] = ex.RetryAfterSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(ex.StatusCode, new OtpErrorResponse(ex.Code, ex.RetryAfterSeconds));
        }
        if (customer == null) return BadRequest(new IdentityCodeResponse("invalid_code"));
        var pair = await tokens.IssueAsync(customer, ct);
        if (pair == null) return BadRequest(new IdentityCodeResponse("invalid_code"));

        // Do not run the native browser sign-in/guest-cart transfer pipeline.
        customer.LastLoginDateUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var required = await otp.IsProfileIncompleteAsync(customer.Id, ct);
        return Ok(new MobileSmsSignInResponse(pair.AccessToken, pair.RefreshToken, "Bearer", pair.ExpiresIn,
            required, required ? new[] { "guildId" } : Array.Empty<string>()));
    }
}
