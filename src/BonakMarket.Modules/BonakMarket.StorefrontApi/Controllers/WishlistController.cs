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
    public async Task<ActionResult<List<CartItemDto>>> Get() => Ok(await cart.ItemsAsync(ShoppingCartType.Wishlist));

    [HttpPost("items")]
    public async Task<ActionResult<List<CartItemDto>>> Add([FromBody] AddCartItem input, CancellationToken ct) =>
        Result(await cart.AddAsync(input, ct, ShoppingCartType.Wishlist));

    [HttpPut("items/{id:int}")]
    public async Task<ActionResult<List<CartItemDto>>> Update(int id, [FromBody] UpdateCartItem input, CancellationToken ct) =>
        Result(await cart.UpdateAsync(id, input.Quantity, ct, ShoppingCartType.Wishlist));

    [HttpDelete("items/{id:int}")]
    public async Task<ActionResult<List<CartItemDto>>> Delete(int id, CancellationToken ct) =>
        Result(await cart.DeleteAsync(id, ct, ShoppingCartType.Wishlist));

    [HttpPost("items/{id:int}/cart")]
    [StorefrontPermission(StorefrontPermissions.Cart)]
    [StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
    public async Task<ActionResult<List<CartItemDto>>> ToCart(int id, CancellationToken ct) =>
        Result(await cart.TransferAsync(id, ShoppingCartType.Wishlist, ct));

    private ActionResult<List<CartItemDto>> Result(CartChange result)
    {
        if (result.Status == 200) return Ok(result.Items);
        var problem = new ProblemDetails { Status = result.Status, Title = result.Code };
        if (result.Warnings?.Count > 0) problem.Extensions["warnings"] = result.Warnings;
        return new ObjectResult(problem) { StatusCode = result.Status, ContentTypes = { "application/problem+json" } };
    }
}
