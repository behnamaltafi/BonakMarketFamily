using System.Threading;
using System.Threading.Tasks;

namespace BonakMarket.Core.Identity;

/// <summary>Optional SMS-backed credentials for the storefront mobile client.</summary>
public interface IMobileApiTokenService
{
    bool IsEnabled { get; }
    Task<MobileApiTokenPair> IssueAsync(Customer customer, CancellationToken ct);
    Task<MobileApiTokenPair> RefreshAsync(string refreshToken, CancellationToken ct);
    Task<bool> RevokeAsync(string accessToken, CancellationToken ct);
    Task<Customer> ValidateAsync(string accessToken, CancellationToken ct);
}

public sealed record MobileApiTokenPair(string AccessToken, string RefreshToken, int ExpiresIn);
