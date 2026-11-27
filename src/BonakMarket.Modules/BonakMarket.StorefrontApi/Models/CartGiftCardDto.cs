#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CartGiftCardDto(int Id, string MaskedCode, decimal? UsableAmount, decimal? AppliedAmount);
