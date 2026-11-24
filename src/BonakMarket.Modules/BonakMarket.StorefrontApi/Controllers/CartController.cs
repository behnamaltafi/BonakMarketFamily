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
public sealed class CartController(CartFacade cart) : StorefrontController
{
    [HttpGet]
    public async Task<ActionResult<List<CartItemDto>>> Get() => Ok(await cart.ItemsAsync());

    [HttpPost("items")]
    public async Task<ActionResult<List<CartItemDto>>> Add([FromBody] AddCartItem request, CancellationToken ct) =>
        Result(await cart.AddAsync(request, ct));

    [HttpPut("items/{id:int}")]
    public async Task<ActionResult<List<CartItemDto>>> Update(int id, [FromBody] UpdateCartItem request, CancellationToken ct) =>
        Result(await cart.UpdateAsync(id, request.Quantity, ct));

    [HttpDelete("items/{id:int}")]
    public async Task<ActionResult<List<CartItemDto>>> Delete(int id, CancellationToken ct) =>
        Result(await cart.DeleteAsync(id, ct));

    [HttpGet("total")]
    [StorefrontPermission(Permissions.Catalog.DisplayPrice)]
    public async Task<ActionResult<CartTotalDto>> Total() => Ok(await cart.TotalAsync());

    [HttpPost("coupon")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> ApplyCoupon([FromBody] CouponRequest request, CancellationToken ct) =>
        await cart.ApplyCouponAsync(request.Code, ct) ? NoContent() : Error(422, "coupon_not_applicable");

    [HttpDelete("coupon")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoveCoupon(CancellationToken ct)
    {
        await cart.RemoveCouponAsync(ct);
        return NoContent();
    }

    [HttpPost("items/{id:int}/wishlist")]
    [StorefrontPermission(StorefrontPermissions.Wishlist)]
    [StorefrontPermission(Permissions.Cart.AccessWishlist)]
    public async Task<ActionResult<List<CartItemDto>>> ToWishlist(int id, CancellationToken ct) =>
        Result(await cart.TransferAsync(id, ShoppingCartType.ShoppingCart, ct));

    [HttpPost("gift-cards")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ApplyGiftCard([FromBody] CouponRequest input, CancellationToken ct) =>
        await cart.ApplyGiftCardAsync(input.Code, ct) ? NoContent() : Error(422, "gift_card_not_applicable");

    [HttpDelete("gift-cards/{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoveGiftCard(int id, CancellationToken ct) =>
        await cart.RemoveGiftCardAsync(id, ct) ? NoContent() : Error(404, "gift_card_not_applied");

    [HttpPut("reward-points")]
    [StorefrontPermission(StorefrontPermissions.RewardPoints)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RewardPoints([FromBody] UseRewardPointsRequest input, CancellationToken ct) =>
        await cart.SetRewardPointsAsync(input.Use, ct) ? NoContent() : Error(403, "reward_points_disabled");

    private ActionResult<List<CartItemDto>> Result(CartChange result)
    {
        if (result.Status == 200) return Ok(result.Items);
        var problem = new ProblemDetails { Status = result.Status, Title = result.Code };
        if (result.Warnings?.Count > 0) problem.Extensions["warnings"] = result.Warnings;
        return new ObjectResult(problem) { StatusCode = result.Status, ContentTypes = { "application/problem+json" } };
    }
}
