using Microsoft.EntityFrameworkCore;
using BonakMarket.Core;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

// An application adapter, not a second cart engine. All cart writes go through the native service.
public sealed class CartFacade(SmartDbContext db, IShoppingCartService cart,
    IProductAttributeMaterializer attributes, StorefrontRequestContext request, IWorkContext work,
    ShoppingCartSettings settings, CartStateReader reader, CartAdjustments adjustments)
{

    public Task<bool> CanDisplayPriceAsync() => reader.CanDisplayPriceAsync();

    public Task<CartStateDto> StateAsync(CancellationToken ct, IReadOnlyList<string> operationWarnings = null) =>
        reader.StateAsync(ct, operationWarnings);

    public Task<WishlistStateDto> WishlistStateAsync(CancellationToken ct, IReadOnlyList<string> operationWarnings = null) =>
        reader.WishlistStateAsync(ct, operationWarnings);

    public Task<List<CartItemDto>> ItemsAsync(ShoppingCartType type = ShoppingCartType.ShoppingCart) => reader.ItemsAsync(type);

    public async Task<CartChange> AddAsync(AddCartItem input, CancellationToken ct, ShoppingCartType type = ShoppingCartType.ShoppingCart)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductVariantAttributeValues)
            .FirstOrDefaultAsync(x => x.Id == input.ProductId, ct);
        if (product == null) return new(404, "product_not_found");
        var selection = await ProductSelection.CreateAsync(product, input.Variants, attributes);
        if (selection.Status != 200) return new(selection.Status, selection.Code, selection.Warnings);
        var context = new AddToCartContext
        {
            Customer = request.Customer, Product = product, StoreId = request.StoreId,
            CartType = type, Quantity = input.Quantity, RawAttributes = selection.Selection.AsJson(),
            AutomaticallyAddRequiredProducts = product.RequireOtherProducts && product.AutomaticallyAddRequiredProducts,
            AutomaticallyAddBundleProducts = true
        };
        if (!await cart.AddToCartAsync(context)) return new(422, "cart_validation_failed", context.Warnings.ToList());
        return new(200);
    }

    public async Task<CartChange> UpdateAsync(int id, int? quantity, CancellationToken ct, ShoppingCartType type = ShoppingCartType.ShoppingCart, bool? active = null)
    {
        var row = await StorefrontScope.Cart(db, request.Customer, request.StoreId, type)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ParentItemId == null, ct);
        if (row == null) return new(404, "cart_item_not_found");
        if (!quantity.HasValue && !active.HasValue) return new(400, "cart_update_required");
        if (active.HasValue && (type != ShoppingCartType.ShoppingCart || !settings.AllowActivatableCartItems))
            return new(409, "cart_activation_unsupported");
        if (!await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AnyAsync(x => x.Id == row.ProductId, ct)) return new(409, "product_unavailable");
        // Native UpdateCartItem persists quantity even with validation warnings.
        var warnings = await cart.UpdateCartItemAsync(request.Customer, id, quantity, active, true);
        return new(200, Warnings: warnings.ToList());
    }

    public async Task<CartChange> DeleteAsync(int id, CancellationToken ct, ShoppingCartType type = ShoppingCartType.ShoppingCart)
    {
        var item = await StorefrontScope.Cart(db, request.Customer, request.StoreId, type).Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && x.ParentItemId == null, ct);
        if (item == null) return new(404, "cart_item_not_found");
        await cart.DeleteCartItemAsync(item, true, true);
        return new(200);
    }

    public async Task<CartChange> TransferAsync(int id, ShoppingCartType source, CancellationToken ct)
    {
        var native = await cart.GetCartAsync(request.Customer, source, request.StoreId, activeOnly: null);
        var item = native.Items.FirstOrDefault(x => x.Item.Id == id);
        if (item == null) return new(404, "cart_item_not_found");
        if (!await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AnyAsync(x => x.Id == item.Item.ProductId, ct)) return new(404, "product_not_found");
        var target = source == ShoppingCartType.Wishlist ? ShoppingCartType.ShoppingCart : ShoppingCartType.Wishlist;
        var context = new AddToCartContext
        {
            Customer = request.Customer, StoreId = request.StoreId, CartType = target,
            Product = item.Item.Product, RawAttributes = item.Item.RawAttributes,
            CustomerEnteredPrice = new(item.Item.CustomerEnteredPrice, work.WorkingCurrency),
            Quantity = item.Item.Quantity, BundleItem = item.Item.BundleItem,
            ChildItems = item.ChildItems.Select(x => x.Item).ToList(),
            AutomaticallyAddRequiredProducts = item.Item.Product.RequireOtherProducts &&
                item.Item.Product.AutomaticallyAddRequiredProducts
        };
        if (!await cart.CopyAsync(context)) return new(422, "cart_validation_failed", context.Warnings.ToList());
        if (settings.MoveItemsFromWishlistToCart) await cart.DeleteCartItemAsync(item.Item);
        return new(200);
    }

    public Task<bool> SetRewardPointsAsync(bool use, CancellationToken ct) => adjustments.SetRewardPointsAsync(use, ct);

    public Task<bool> ApplyGiftCardAsync(string code, CancellationToken ct) => adjustments.ApplyGiftCardAsync(code, ct);

    public Task<bool> RemoveGiftCardAsync(int id, CancellationToken ct) => adjustments.RemoveGiftCardAsync(id, ct);

    public Task ClearGiftCardsAsync(CancellationToken ct) => adjustments.ClearGiftCardsAsync(ct);

    public Task<bool> ApplyCouponAsync(string code, CancellationToken ct) => adjustments.ApplyCouponAsync(code, ct);

    public Task RemoveCouponAsync(CancellationToken ct) => adjustments.RemoveCouponAsync(ct);
}
