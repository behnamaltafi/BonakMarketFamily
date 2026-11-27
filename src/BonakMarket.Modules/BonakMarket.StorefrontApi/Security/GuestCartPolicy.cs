using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using BonakMarket.Core.Identity;

namespace BonakMarket.StorefrontApi.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowGuestCartAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public sealed class CreateGuestCartAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public sealed class MergeGuestCartAttribute : Attribute { }

internal static class GuestCartPolicy
{
    public const string Header = "X-Guest-Cart-Token";
    public static bool Enabled(IConfiguration config) => config?.GetValue("StorefrontApi:EnableGuestCart", true) ?? true;
    public static int ExpirationDays(IConfiguration config) => Math.Clamp(config?.GetValue("StorefrontApi:GuestCartExpirationDays", 30) ?? 30, 1, 90);
    public static bool IsGuest(Customer customer) => customer != null && customer.Id > 0 &&
        customer.Active && !customer.Deleted && !customer.IsSystemAccount && !customer.IsBot() &&
        customer.IsGuest() && !customer.IsRegistered();
    public static string Generate() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string token)
    {
        if (token == null || token.Length != 43 ||
            token.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
            return null;
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(token)));
    }
    public static string ReadHash(HttpRequest request) =>
        request.Headers.TryGetValue(Header, out var values) && values.Count == 1 ? Hash(values[0]) : null;
}
