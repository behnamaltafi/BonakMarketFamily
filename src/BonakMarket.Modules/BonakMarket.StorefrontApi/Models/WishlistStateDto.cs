#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record WishlistStateDto(string Revision, IReadOnlyList<CartLineDto> Items, int ItemCount, int TotalQuantity,
    IReadOnlyList<string> Warnings);
