#nullable enable
using BonakMarket.StorefrontApi.Models;
namespace BonakMarket.StorefrontApi.Services;

internal sealed record CheckoutAttempt(Guid SessionId, Guid Key, int Revision, decimal ExpectedTotal,
    string Currency, string Fingerprint, DateTime StartedOnUtc, string Status = "started")
{
    public bool Matches(Guid key, ConfirmCheckoutRequest input) =>
        Key == key && Revision == input.Revision && ExpectedTotal == input.ExpectedOrderTotal &&
        Currency == input.CurrencyCode && Fingerprint == input.CartFingerprint;
}
