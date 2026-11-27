#nullable enable
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Services;
using BonakMarket.Identity.Security;

namespace BonakMarket.Identity.Controllers;

/// <summary>Token rotation and current-session revocation; never signs in or out of MVC.</summary>
[ApiController, AllowAnonymous, IgnoreAntiforgeryToken]
[ApiExplorerSettings(GroupName = "identity")]
[Route("api/identity/v1/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthApiController(IMobileApiTokenService tokens, ICustomerSessionRevoker revoker) : CustomerAccountApiController
{
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(IdentityTokenResponse), 200)]
    public async Task<IActionResult> Refresh([FromBody] MobileRefreshRequest request, CancellationToken ct)
    {
        if (!IdentityTransportPolicy.IsAllowed(Request))
            return BadRequest(new IdentityCodeResponse("https_required"));
        var pair = tokens.IsEnabled ? await tokens.RefreshAsync(request.RefreshToken, ct) : null;
        return pair == null ? Unauthorized(new IdentityCodeResponse("invalid_refresh_token"))
            : Ok(new IdentityTokenResponse(pair.AccessToken, pair.RefreshToken, pair.ExpiresIn, "Bearer"));
    }

    /// <summary>Revokes the current session via a valid access token, or current refresh token with no Authorization header.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CustomerLogoutRequest? input, CancellationToken ct)
    {
        if (!IdentityTransportPolicy.IsAllowed(Request))
            return BadRequest(new IdentityCodeResponse("https_required"));
        if (Request.Headers.ContainsKey("Authorization"))
        {
            if (!string.IsNullOrEmpty(input?.RefreshToken))
                return BadRequest(new IdentityCodeResponse("ambiguous_logout_credentials"));
            var (_, error) = await RequireAccountAsync();
            if (error != null) return error;
            if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var header) ||
                !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
                !await tokens.RevokeAsync(header.Parameter, ct))
                return Unauthorized(new IdentityCodeResponse("invalid_access_token"));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(input?.RefreshToken))
            {
                Response.Headers.WWWAuthenticate = "Bearer";
                return Unauthorized(new IdentityCodeResponse("authentication_required"));
            }
            if (!await revoker.RevokeRefreshAsync(input.RefreshToken, ct))
                return Unauthorized(new IdentityCodeResponse("invalid_refresh_token"));
        }
        return NoContent();
    }
}
