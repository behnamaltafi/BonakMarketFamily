#nullable enable
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Services;

internal sealed record CheckoutDraftLoadResult(CheckoutDraft? Draft = null, CartStateDto? Cart = null,
    ShoppingCart? Native = null, StorefrontFailure? Error = null);
