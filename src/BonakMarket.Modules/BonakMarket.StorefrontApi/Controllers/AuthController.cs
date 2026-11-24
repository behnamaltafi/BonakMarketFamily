using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Common.DTO;
using BonakMarket.Core.Identity;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/auth")]
public sealed class AuthController : StorefrontController
{
    // OTP request, verify and refresh deliberately reuse /api/sms-login.
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResult), 200)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Headers.ContainsKey("Authorization"))
        {
            var tokens = HttpContext.RequestServices.GetService<IMobileApiTokenService>();
            if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var header) ||
                tokens == null || !await tokens.RevokeAsync(header.Parameter, ct))
                return Error(401, "invalid_access_token");
        }
        else
        {
            await HttpContext.RequestServices.GetRequiredService<SignInManager<Customer>>().SignOutAsync();
        }
        return Ok(new ApiResult());
    }
}
