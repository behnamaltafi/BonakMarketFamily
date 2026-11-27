namespace BonakMarket.StorefrontApi.Models;

public sealed record GuestCartCreatedDto(string GuestCartToken, DateTime ExpiresOnUtc);
