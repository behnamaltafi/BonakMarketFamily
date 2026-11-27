using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/cart")]
[StorefrontPermission(StorefrontPermissions.Cart)]
[StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
public sealed class CartController(CartFacade cart, GuestCartService guests) : StorefrontController
{
    [HttpPost("guest"), CreateGuestCart]
    public async Task<ActionResult<GuestCartCreatedDto>> CreateGuest(CancellationToken ct)
    {
        var created = await guests.CreateAsync(ct);
        return created == null ? Error(503, "guest_cart_unavailable") : Ok(created);
    }

    [HttpPost("merge"), MergeGuestCart]
    public async Task<ActionResult<CartStateDto>> Merge(CancellationToken ct)
    {
        var hash = HttpContext.Items.TryGetValue(GuestCartPolicy.MergeAllowed, out var value) ? value as string : null;
        var result = await guests.MergeAsync(hash, ct);
        if (result.Status == 200)
        {
            var state = await cart.StateAsync(ct);
            return Ok(result.Warning == null ? state : state with { Warnings = state.Warnings.Append(result.Warning).Distinct().ToArray() });
        }
        var problem = new ProblemDetails { Status = result.Status, Title = result.Code };
        if (result.Conflicts?.Count > 0) problem.Extensions["conflicts"] = result.Conflicts;
        return new ObjectResult(problem) { StatusCode = result.Status, ContentTypes = { "application/problem+json" } };
    }

    [HttpGet, AllowGuestCart]
    public async Task<ActionResult<CartStateDto>> Get(CancellationToken ct) => Ok(await cart.StateAsync(ct));

    [HttpGet("items"), AllowGuestCart]
    public async Task<ActionResult<List<CartItemDto>>> Items() => Ok(await cart.ItemsAsync());

    [HttpPost("items"), AllowGuestCart]
    public async Task<ActionResult<CartStateDto>> Add([FromBody] AddCartItem request, CancellationToken ct) =>
        await Result(await cart.AddAsync(request, ct), ct);

    [HttpPut("items/{id:int}"), AllowGuestCart]
    public async Task<ActionResult<CartStateDto>> Update(int id, [FromBody] UpdateCartItem request, CancellationToken ct) =>
        await Result(await cart.UpdateAsync(id, request.Quantity, ct, active: request.Active), ct);

    [HttpDelete("items/{id:int}"), AllowGuestCart]
    public async Task<ActionResult<CartStateDto>> Delete(int id, CancellationToken ct) =>
        await Result(await cart.DeleteAsync(id, ct), ct);

    [HttpGet("total"), AllowGuestCart]
    [StorefrontPermission(Permissions.Catalog.DisplayPrice)]
    public async Task<ActionResult<CartTotalDto>> Total(CancellationToken ct)
    {
        var state = await cart.StateAsync(ct);
        if (state.Summary.CurrencyCode == null) return Error(422, "cart_validation_failed");
        return Ok(new CartTotalDto(state.Summary.EstimatedTotal, state.Summary.OrderDiscountAmount ?? 0m, state.Summary.CurrencyCode));
    }

    [HttpPost("coupon"), AllowGuestCart]
    [ProducesResponseType(typeof(CartStateDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<ActionResult<CartStateDto>> ApplyCoupon([FromBody] CouponRequest request, CancellationToken ct) =>
        await cart.ApplyCouponAsync(request.Code, ct) ? Ok(await cart.StateAsync(ct)) : Error(422, "coupon_not_applicable");

    [HttpDelete("coupon"), AllowGuestCart]
    public async Task<ActionResult<CartStateDto>> RemoveCoupon(CancellationToken ct)
    {
        await cart.RemoveCouponAsync(ct);
        return Ok(await cart.StateAsync(ct));
    }

    [HttpPost("items/{id:int}/wishlist")]
    [StorefrontPermission(StorefrontPermissions.Wishlist)]
    [StorefrontPermission(Permissions.Cart.AccessWishlist)]
    public async Task<ActionResult<CartTransferDto>> ToWishlist(int id, CancellationToken ct)
    {
        var result = await cart.TransferAsync(id, ShoppingCartType.ShoppingCart, ct);
        if (result.Status != 200) return await CartProblemResponse.FailureAsync(result, cart);
        return Ok(new CartTransferDto(await cart.StateAsync(ct), await cart.WishlistStateAsync(ct)));
    }

    [HttpPost("gift-cards")]
    public async Task<ActionResult<CartStateDto>> ApplyGiftCard([FromBody] CouponRequest input, CancellationToken ct) =>
        await cart.ApplyGiftCardAsync(input.Code, ct) ? Ok(await cart.StateAsync(ct)) : Error(422, "gift_card_not_applicable");

    [HttpDelete("gift-cards/{id:int}")]
    public async Task<ActionResult<CartStateDto>> RemoveGiftCard(int id, CancellationToken ct) =>
        await cart.RemoveGiftCardAsync(id, ct) ? Ok(await cart.StateAsync(ct)) : Error(404, "gift_card_not_applied");

    [HttpDelete("gift-cards")]
    public async Task<ActionResult<CartStateDto>> ClearGiftCards(CancellationToken ct)
    {
        await cart.ClearGiftCardsAsync(ct);
        return Ok(await cart.StateAsync(ct));
    }

    [HttpPut("reward-points")]
    [StorefrontPermission(StorefrontPermissions.RewardPoints)]
    public async Task<ActionResult<CartStateDto>> RewardPoints([FromBody] UseRewardPointsRequest input, CancellationToken ct) =>
        await cart.SetRewardPointsAsync(input.Use, ct) ? Ok(await cart.StateAsync(ct)) : Error(403, "reward_points_disabled");

    private async Task<ActionResult<CartStateDto>> Result(CartChange result, CancellationToken ct)
    {
        if (result.Status == 200) return Ok(await cart.StateAsync(ct, result.Warnings));
        return await CartProblemResponse.FailureAsync(result, cart);
    }

}
