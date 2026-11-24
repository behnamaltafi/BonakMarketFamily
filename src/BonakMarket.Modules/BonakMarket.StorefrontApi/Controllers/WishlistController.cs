using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/wishlist")]
[StorefrontPermission(StorefrontPermissions.Wishlist)]
[StorefrontPermission(Permissions.Cart.AccessWishlist)]
public sealed class WishlistController(CartFacade cart) : StorefrontController
{
    [HttpGet]
    public async Task<ActionResult<WishlistStateDto>> Get(CancellationToken ct) => Ok(await cart.WishlistStateAsync(ct));

    [HttpGet("items")]
    public async Task<ActionResult<List<CartItemDto>>> Items() => Ok(await cart.ItemsAsync(ShoppingCartType.Wishlist));

    [HttpPost("items")]
    public async Task<ActionResult<WishlistStateDto>> Add([FromBody] AddCartItem input, CancellationToken ct) =>
        await Result(await cart.AddAsync(input, ct, ShoppingCartType.Wishlist), ct);

    [HttpPut("items/{id:int}")]
    public async Task<ActionResult<WishlistStateDto>> Update(int id, [FromBody] UpdateCartItem input, CancellationToken ct) =>
        await Result(await cart.UpdateAsync(id, input.Quantity, ct, ShoppingCartType.Wishlist, input.Active), ct);

    [HttpDelete("items/{id:int}")]
    public async Task<ActionResult<WishlistStateDto>> Delete(int id, CancellationToken ct) =>
        await Result(await cart.DeleteAsync(id, ct, ShoppingCartType.Wishlist), ct);

    [HttpPost("items/{id:int}/cart")]
    [StorefrontPermission(StorefrontPermissions.Cart)]
    [StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
    public async Task<ActionResult<CartTransferDto>> ToCart(int id, CancellationToken ct)
    {
        var result = await cart.TransferAsync(id, ShoppingCartType.Wishlist, ct);
        if (result.Status != 200) return await Failure(result);
        return Ok(new CartTransferDto(await cart.StateAsync(ct), await cart.WishlistStateAsync(ct)));
    }

    private async Task<ActionResult<WishlistStateDto>> Result(CartChange result, CancellationToken ct)
    {
        if (result.Status == 200) return Ok(await cart.WishlistStateAsync(ct, result.Warnings));
        return await Failure(result);
    }

    private async Task<ObjectResult> Failure(CartChange result)
    {
        var problem = new ProblemDetails { Status = result.Status, Title = result.Code };
        if (result.Warnings?.Count > 0 && await cart.CanDisplayPriceAsync()) problem.Extensions["warnings"] = result.Warnings;
        return new ObjectResult(problem) { StatusCode = result.Status, ContentTypes = { "application/problem+json" } };
    }
}
