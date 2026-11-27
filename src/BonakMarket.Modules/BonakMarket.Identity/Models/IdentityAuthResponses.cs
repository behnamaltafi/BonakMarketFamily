#nullable enable

using System.Text.Json.Serialization;

namespace BonakMarket.Identity.Models;

public sealed record IdentityCodeResponse(
    [property: JsonPropertyName("code")] string Code);

public sealed record OtpErrorResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("retryAfter"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RetryAfter = null);

public sealed record IdentityTokenResponse(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("refreshToken")] string RefreshToken,
    [property: JsonPropertyName("expiresIn")] int ExpiresIn,
    [property: JsonPropertyName("tokenType")] string TokenType);

