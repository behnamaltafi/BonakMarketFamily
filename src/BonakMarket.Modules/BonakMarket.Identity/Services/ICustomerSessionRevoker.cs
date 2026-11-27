namespace BonakMarket.Identity.Services;

/// <summary>Revokes a session by its current refresh credential without rotating it.</summary>
public interface ICustomerSessionRevoker
{
    Task<bool> RevokeRefreshAsync(string refreshToken, CancellationToken ct);
}
