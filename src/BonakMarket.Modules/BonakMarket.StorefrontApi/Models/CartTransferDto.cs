#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CartTransferDto(CartStateDto Cart, WishlistStateDto Wishlist);
