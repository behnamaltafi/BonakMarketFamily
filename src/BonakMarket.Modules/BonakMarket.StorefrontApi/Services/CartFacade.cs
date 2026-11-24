using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

// An application adapter, not a second cart engine. All cart writes go through the native service.
public sealed class CartFacade(SmartDbContext db, IShoppingCartService cart, IOrderCalculationService calculations,
    IProductAttributeMaterializer attributes, StorefrontRequestContext request)
{
    public async Task<List<CartItemDto>> ItemsAsync()
    {
        var native = await cart.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId, activeOnly: null);
        var items = new List<CartItemDto>();
        foreach (var item in native.Items) Add(item);
        return items;

        void Add(OrganizedShoppingCartItem item)
        {
            var x = item.Item;
            items.Add(new CartItemDto(x.Id, x.ProductId, x.Quantity, x.Active, x.ParentItemId));
            foreach (var child in item.ChildItems) Add(child);
        }
    }

    public async Task<CartChange> AddAsync(AddCartItem input, CancellationToken ct)
    {
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forSearch: true)
            .Include(x => x.ProductVariantAttributes).ThenInclude(x => x.ProductVariantAttributeValues)
            .FirstOrDefaultAsync(x => x.Id == input.ProductId, ct);
        if (product == null) return new(404, "product_not_found");
        if (product.CustomerEntersPrice || product.IsGiftCard || product.CallForPrice || product.DisableBuyButton ||
            product.ProductType != ProductType.SimpleProduct)
            return new(409, "product_requires_configuration");

        var query = new ProductVariantQuery();
        var choices = input.Variants ?? new List<VariantChoice>();
        if (choices.Count > 30 || choices.Any(x => x == null) ||
            choices.Select(x => (x.VariantAttributeId, x.ValueId)).Distinct().Count() != choices.Count)
            return new(400, "invalid_variants");
        foreach (var group in choices.GroupBy(x => x.VariantAttributeId))
        {
            var attribute = product.ProductVariantAttributes.FirstOrDefault(x => x.Id == group.Key);
            if (attribute == null || attribute.AttributeControlType is not (AttributeControlType.DropdownList or
                AttributeControlType.RadioList or AttributeControlType.Boxes or AttributeControlType.Checkboxes))
                return new(400, "invalid_variant_attribute");
            if (attribute.AttributeControlType != AttributeControlType.Checkboxes && group.Count() > 1)
                return new(400, "multiple_variant_values");
            foreach (var choice in group)
            {
                if (!attribute.ProductVariantAttributeValues.Any(x => x.Id == choice.ValueId))
                    return new(400, "invalid_variant_value");
                query.AddVariant(new ProductVariantQueryItem { ProductId = product.Id, AttributeId = attribute.ProductAttributeId,
                    VariantAttributeId = attribute.Id, Value = choice.ValueId.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
        }
        // Do not allow a client-supplied download GUID or other raw attributes.
        if (product.ProductVariantAttributes.Any(x => x.IsRequired && x.AttributeControlType is
            (AttributeControlType.FileUpload or AttributeControlType.TextBox or AttributeControlType.MultilineTextbox or AttributeControlType.Datepicker)))
            return new(409, "unsupported_required_attribute");

        var (selection, warnings) = await attributes.CreateAttributeSelectionAsync(query, product.ProductVariantAttributes,
            product.Id, 0, getFilesFromRequest: false);
        if (warnings.Count > 0) return new(422, "invalid_variants", warnings);
        var context = new AddToCartContext
        {
            Customer = request.Customer, Product = product, StoreId = request.StoreId,
            CartType = ShoppingCartType.ShoppingCart, Quantity = input.Quantity, RawAttributes = selection.AsJson(),
            AutomaticallyAddRequiredProducts = product.RequireOtherProducts && product.AutomaticallyAddRequiredProducts,
            AutomaticallyAddBundleProducts = true
        };
        if (!await cart.AddToCartAsync(context)) return new(422, "cart_validation_failed", context.Warnings.ToList());
        return new(200, Items: await ItemsAsync());
    }

    public async Task<CartChange> UpdateAsync(int id, int quantity, CancellationToken ct)
    {
        if (!await StorefrontScope.Cart(db, request.Customer, request.StoreId).AnyAsync(x => x.Id == id && x.ParentItemId == null, ct))
            return new(404, "cart_item_not_found");
        var warnings = await cart.UpdateCartItemAsync(request.Customer, id, quantity, null, true);
        if (warnings.Count > 0) return new(422, "cart_validation_failed", warnings.ToList());
        return new(200, Items: await ItemsAsync());
    }

    public async Task<CartChange> DeleteAsync(int id, CancellationToken ct)
    {
        var item = await StorefrontScope.Cart(db, request.Customer, request.StoreId).Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id && x.ParentItemId == null, ct);
        if (item == null) return new(404, "cart_item_not_found");
        await cart.DeleteCartItemAsync(item, true, true);
        return new(200, Items: await ItemsAsync());
    }

    public async Task<CartTotalDto> TotalAsync()
    {
        var current = await cart.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId);
        var result = await calculations.GetShoppingCartTotalAsync(current, ShoppingCartTotalOptions.Default);
        return new CartTotalDto(result.Total?.Amount, result.DiscountAmount.Amount, result.DiscountAmount.Currency.CurrencyCode);
    }

    public async Task<bool> ApplyCouponAsync(string code, CancellationToken ct)
    {
        var current = await cart.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId);
        var (applied, _) = await calculations.ApplyDiscountCouponAsync(current, code);
        await db.SaveChangesAsync(ct);
        return applied;
    }

    public async Task RemoveCouponAsync(CancellationToken ct)
    {
        request.Customer.GenericAttributes.DiscountCouponCode = null;
        await db.SaveChangesAsync(ct);
    }
}

public sealed record CartChange(int Status, string Code = null, List<string> Warnings = null, List<CartItemDto> Items = null);
