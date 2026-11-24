#nullable enable

using System.Text.Json.Serialization;

namespace BonakMarket.Identity.Models;

public sealed record IdentityCodeResponse(
    [property: JsonPropertyName("code")] string Code);

public sealed record IdentitySessionResponse(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("isAuthenticated")] bool IsAuthenticated,
    [property: JsonPropertyName("profileRequired")] bool ProfileRequired,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("requestToken")] string? RequestToken,
    [property: JsonPropertyName("headerName")] string? HeaderName);

public sealed record IdentityTokenResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("refreshToken")] string RefreshToken,
    [property: JsonPropertyName("expiresIn")] int ExpiresIn,
    [property: JsonPropertyName("tokenType")] string TokenType);

/// <summary>Tokens are returned only if requested and successfully issued; preserve the cookie-only JSON shape.</summary>
public sealed record SmsSignInResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("profileRequired")] bool ProfileRequired,
    [property: JsonPropertyName("profilePath")] string ProfilePath,
    [property: JsonPropertyName("accessToken"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AccessToken = null,
    [property: JsonPropertyName("refreshToken"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RefreshToken = null,
    [property: JsonPropertyName("expiresIn"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ExpiresIn = null,
    [property: JsonPropertyName("tokenType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TokenType = null);
