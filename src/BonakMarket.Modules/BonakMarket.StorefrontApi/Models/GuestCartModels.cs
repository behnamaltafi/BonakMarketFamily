namespace BonakMarket.StorefrontApi.Models;

public sealed record GuestCartCreatedDto(string GuestCartToken, DateTime ExpiresOnUtc);
public sealed record GuestCartConflictDto(int ItemId, int ProductId, string Code);
