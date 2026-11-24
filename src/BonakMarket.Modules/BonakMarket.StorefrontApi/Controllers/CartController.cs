using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/cart")]
public sealed class CartController(SmartDbContext db, IShoppingCartService cart) : StorefrontController
{
    private Task<bool> CanAccess() => PermissionsService.AuthorizeAsync(Permissions.Cart.AccessShoppingCart, Customer);
    private Task<List<CartItemDto>> Items() => StorefrontScope.Cart(db, Customer, StoreId).AsNoTracking()
        .OrderBy(x => x.Id).Select(x => new CartItemDto(x.Id, x.ProductId, x.Quantity, x.Active, x.ParentItemId)).ToListAsync();

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!await CanAccess()) return Error(403, "cart_access_denied");
        return Success(await Items());
    }
    [HttpPost("items")]
    public async Task<IActionResult> Add([FromBody] AddCartItem request)
    {
        if (!await CanAccess()) return Error(403, "cart_access_denied");
        var product = await StorefrontScope.Products(db, Customer, StoreId)
            .Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductAttribute)
            .FirstOrDefaultAsync(x => x.Id == request.ProductId);
        if (product == null) return Error(404, "product_not_found");
        if (product.CustomerEntersPrice || product.IsGiftCard || product.CallForPrice ||
            product.ProductVariantAttributes.Any() || product.DisableBuyButton)
            return Error(409, "product_requires_configuration");
        var ctx = new AddToCartContext { Customer = Customer, Product = product, StoreId = StoreId,
            CartType = ShoppingCartType.ShoppingCart, Quantity = request.Quantity,
            AutomaticallyAddRequiredProducts = product.RequireOtherProducts && product.AutomaticallyAddRequiredProducts,
            AutomaticallyAddBundleProducts = true };
        if (!await cart.AddToCartAsync(ctx))
            return StatusCode(422, new Common.DTO.ApiResult("cart_validation_failed", false, ctx.Warnings, 422));
        return Success(await Items());
    }
    [HttpPut("items/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCartItem request)
    {
        if (!await CanAccess()) return Error(403, "cart_access_denied");
        if (!await StorefrontScope.Cart(db, Customer, StoreId).AnyAsync(x => x.Id == id && x.ParentItemId == null))
            return Error(404, "cart_item_not_found");
        var warnings = await cart.UpdateCartItemAsync(Customer, id, request.Quantity, null, true);
        if (warnings.Count > 0)
            return StatusCode(422, new Common.DTO.ApiResult("cart_validation_failed", false, warnings, 422));
        return Success(await Items());
    }
    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await CanAccess()) return Error(403, "cart_access_denied");
        var item = await StorefrontScope.Cart(db, Customer, StoreId).Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && x.ParentItemId == null);
        if (item == null) return Error(404, "cart_item_not_found");
        await cart.DeleteCartItemAsync(item, true, true);
        return Success(await Items());
    }
}
