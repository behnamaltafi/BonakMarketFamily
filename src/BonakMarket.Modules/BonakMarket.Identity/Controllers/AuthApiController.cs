using System.Net.Http.Headers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Services;

namespace BonakMarket.Identity.Controllers;

/// <summary>Authentication session, refresh and sign-out.</summary>
[ApiController]
[Route("api/identity/v1/auth")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthApiController : CustomerAccountApiController
{
    private readonly ISmsOtpLoginService _otp;

    public AuthApiController(ISmsOtpLoginService otp, SignInManager<Customer> signInManager,
        UserManager<Customer> userManager, IAntiforgery antiforgery)
        : base(signInManager, userManager, antiforgery)
    {
        _otp = otp;
    }

    /// <summary>Gets feature availability, the current session and a CSRF request token.</summary>
    [HttpGet("session"), HttpGet("~/api/sms-login/session"), AllowAnonymous]
    public async Task<IActionResult> Session(CancellationToken ct)
    {
        bool enabled = await _otp.IsEnabledAsync();
        var customer = await GetCustomerAsync();
        if (Request.Headers.ContainsKey("Authorization") && customer is null)
            return Unauthorized();

        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new
        {
            enabled,
            isAuthenticated = customer is not null,
            profileRequired = enabled && customer is not null &&
                await _otp.IsProfileIncompleteAsync(customer.Id, ct),
            phone = enabled && customer is not null ? await _otp.GetVerifiedPhoneAsync(customer.Id, ct) : null,
            requestToken = tokens.RequestToken,
            headerName = tokens.HeaderName
        });
    }

    /// <summary>Rotates a mobile refresh credential. Does not use the web cookie.</summary>
    [HttpPost("refresh"), HttpPost("~/api/sms-login/refresh"), AllowAnonymous, IgnoreAntiforgeryToken]
    public async Task<IActionResult> Refresh([FromBody] MobileRefreshRequest request, CancellationToken ct)
    {
        if (!Request.IsHttps || !await _otp.IsEnabledAsync())
            return Unauthorized();
        var service = HttpContext.RequestServices.GetService<IMobileApiTokenService>();
        var pair = service?.IsEnabled == true ? await service.RefreshAsync(request.RefreshToken, ct) : null;
        return pair == null ? Unauthorized() : Ok(new { accessToken = pair.AccessToken,
            refreshToken = pair.RefreshToken, expiresIn = pair.ExpiresIn, tokenType = "Bearer" });
    }

    [HttpPost("logout"), HttpPost("~/api/sms-login/logout"), AllowAnonymous, IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var (_, error) = await RequireAccountAsync();
        if (error is not null) return error;
        if (Request.Headers.ContainsKey("Authorization"))
        {
            var tokens = HttpContext.RequestServices.GetService<IMobileApiTokenService>();
            if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var header) ||
                tokens == null || !await tokens.RevokeAsync(header.Parameter, ct))
                return Unauthorized();
        }
        else
        {
            await _signInManager.SignOutAsync();
        }
        return NoContent();
    }
}
