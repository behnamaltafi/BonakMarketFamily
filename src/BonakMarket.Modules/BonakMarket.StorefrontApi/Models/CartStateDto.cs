#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CartStateDto(string Revision, IReadOnlyList<CartLineDto> Items, CartSummaryDto Summary,
    string? AppliedCoupon, IReadOnlyList<CartGiftCardDto> AppliedGiftCards, bool UseRewardPoints,
    CartCheckoutDto Checkout, IReadOnlyList<string> Warnings)
{
    public string CartType { get; init; } = "customer";
}
