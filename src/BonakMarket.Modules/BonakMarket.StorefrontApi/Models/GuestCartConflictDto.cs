namespace BonakMarket.StorefrontApi.Models;

public sealed record GuestCartConflictDto(int ItemId, int ProductId, string Code);
