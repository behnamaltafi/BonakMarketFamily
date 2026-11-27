using Microsoft.EntityFrameworkCore;
using BonakMarket.Core;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Checkout.GiftCards;
using BonakMarket.Threading;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Tax;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Security;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

// An application adapter, not a second cart engine. All cart writes go through the native service.
public sealed class CartFacade(SmartDbContext db, IShoppingCartService cart, IOrderCalculationService calculations,
    IProductAttributeMaterializer attributes, StorefrontRequestContext request, IWorkContext work,
    ShoppingCartSettings settings, RewardPointsSettings rewards, IGiftCardService gifts, IDistributedLockProvider locks,
    IPermissionService permissions, IMediaService media, IShoppingCartValidator validator)
{
    public Task<bool> CanDisplayPriceAsync() => permissions.AuthorizeAsync(Permissions.Catalog.DisplayPrice, request.Customer);

    public async Task<CartStateDto> StateAsync(CancellationToken ct, IReadOnlyList<string> operationWarnings = null)
    {
        var snapshot = await ReadSnapshotAsync(ShoppingCartType.ShoppingCart, ct);
        var active = snapshot.Native.WithActiveItemsOnly();
        var warnings = new List<string>(snapshot.CanPrice ? operationWarnings ?? Array.Empty<string>() : Array.Empty<string>());
        var blockers = new List<string>();
        if (!active.HasItems) blockers.Add("cart_empty");
        if (request.IsGuestCart) blockers.Add("authentication_required");
        else if (!await permissions.AuthorizeAsync(StorefrontPermissions.Checkout, request.Customer))
            blockers.Add("checkout_access_denied");
        if (await CustomerGuildPolicy.RequiresGuildAsync(db, request.Customer, ct))
            blockers.Add("profile_guild_required");
        var invalid = snapshot.Lines.Any(x => x.Active && x.Availability != "available");
        if (invalid) blockers.Add("cart_validation_failed");
        if (active.HasItems && !invalid)
        {
            var cartWarnings = new List<string>();
            // Checkout attribute validation can modify customer attributes; never run it on a GET.
            if (!await validator.ValidateCartAsync(active, cartWarnings, false, true))
                blockers.Add("cart_validation_failed");
            if (snapshot.CanPrice) warnings.AddRange(cartWarnings);
        }

        ShoppingCartSubtotal sub = null;
        ShoppingCartTotal total = null;
        decimal? shipping = null, tax = null;
        var includingTax = work.TaxDisplayType == TaxDisplayType.IncludingTax;
        if (snapshot.CanPrice && !invalid)
        {
            sub = await calculations.GetShoppingCartSubtotalAsync(active, includeTax: includingTax, cache: false);
            if (active.HasItems)
            {
                // Native total calculation can persist normalized credit usage. Do not invoke it for
                // an API estimate with a nonzero credit selection; leave total unknown until native checkout.
                if (request.Customer.GenericAttributes.UseCreditBalanceDuringCheckout == 0m)
                    total = await calculations.GetShoppingCartTotalAsync(active, new ShoppingCartTotalOptions
                    {
                        IncludeTax = includingTax, UseRequestCache = false
                    });
                else warnings.Add("credit_balance_requires_native_checkout");
                shipping = (await calculations.GetShoppingCartShippingTotalAsync(active, includingTax)).ShippingTotal?.Amount;
                tax = (await calculations.GetShoppingCartTaxTotalAsync(active)).Price.Amount;
            }
        }

        var lines = WithPrices(snapshot.Lines, sub);
        var applied = total?.AppliedGiftCards?.ToDictionary(x => x.GiftCard.Id, x => x.UsableAmount.Amount);
        var validGifts = await gifts.GetValidGiftCardsAsync(request.StoreId, request.Customer);
        var cards = validGifts.OrderBy(x => x.GiftCard.Id).Select(x => new CartGiftCardDto(x.GiftCard.Id,
            MaskCode(x.GiftCard.GiftCardCouponCode), snapshot.CanPrice ? x.UsableAmount.Amount : null,
            total?.Total == null ? null : applied?.GetValueOrDefault(x.GiftCard.Id) ?? 0m)).ToList();
        var root = lines.Where(x => x.ParentItemId == null).ToList();
        var summary = new CartSummaryDto(root.Count, root.Sum(x => x.Quantity), root.Where(x => x.Active).Sum(x => x.Quantity),
            sub?.SubtotalWithoutDiscount.Currency.CurrencyCode, sub?.SubtotalWithoutDiscount.Amount,
            sub?.DiscountAmount.Amount, sub?.SubtotalWithDiscount.Amount, total?.DiscountAmount.Amount,
            shipping, tax, sub == null ? null : includingTax,
            total?.Total == null ? null : total.AppliedGiftCards?.Sum(x => x.UsableAmount.Amount),
            total?.Total == null ? null : total.RedeemedRewardPointsAmount.Amount,
            total?.Total == null ? null : total.RedeemedRewardPoints,
            total?.Total == null ? null : total.CreditBalance.Amount,
            total?.Total?.Amount, null);
        var state = new CartStateDto("", lines, summary, request.Customer.GenericAttributes.DiscountCouponCode,
            cards, request.Customer.GenericAttributes.UseRewardPointsDuringCheckout,
            new CartCheckoutDto(blockers.Count == 0, active.IsShippingRequired, blockers.Distinct().ToArray()), warnings.Distinct().ToArray())
        { CartType = request.IsGuestCart ? "guest" : "customer" };
        return state with { Revision = Revision(snapshot.Native, state with { Warnings = Array.Empty<string>() }) };
    }

    public async Task<WishlistStateDto> WishlistStateAsync(CancellationToken ct, IReadOnlyList<string> operationWarnings = null)
    {
        var snapshot = await ReadSnapshotAsync(ShoppingCartType.Wishlist, ct);
        var eligible = snapshot.Lines.Where(x => x.ParentItemId == null && x.Availability == "available")
            .Select(x => x.Id).ToHashSet();
        eligible.ExceptWith(snapshot.Lines.Where(x => x.ParentItemId.HasValue && x.Availability != "available")
            .Select(x => x.ParentItemId.Value));
        var priceCart = new ShoppingCart(snapshot.Native, snapshot.Native.Items.Where(x => eligible.Contains(x.Item.Id)));
        var sub = snapshot.CanPrice && priceCart.HasItems
            ? await calculations.GetShoppingCartSubtotalAsync(priceCart, cache: false) : null;
        var lines = WithPrices(snapshot.Lines, sub);
        var root = lines.Where(x => x.ParentItemId == null).ToList();
        var state = new WishlistStateDto("", lines, root.Count, root.Sum(x => x.Quantity),
            snapshot.CanPrice ? operationWarnings ?? Array.Empty<string>() : Array.Empty<string>());
        return state with { Revision = Revision(snapshot.Native, state with { Warnings = Array.Empty<string>() }) };
    }

    private async Task<(ShoppingCart Native, List<CartLineDto> Lines, bool CanPrice)> ReadSnapshotAsync(ShoppingCartType type, CancellationToken ct)
    {
        var native = await cart.GetCartAsync(request.Customer, type, request.StoreId, activeOnly: null);
        var nodes = native.Items.SelectMany(Flatten).OrderBy(x => x.Item.Id).ToList();
        var productIds = nodes.Select(x => x.Item.ProductId).Distinct().ToArray();
        var products = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var pictures = await db.Set<ProductMediaFile>().AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId) && !x.MediaFile.Deleted && !x.MediaFile.Hidden && !x.MediaFile.IsTransient)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id).Select(x => new { x.ProductId, x.MediaFileId }).ToListAsync(ct);
        var pictureIds = pictures.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First().MediaFileId);
        var urls = new Dictionary<int, string>();
        var canPrice = await CanDisplayPriceAsync();
        var target = type == ShoppingCartType.Wishlist ? ShoppingCartType.ShoppingCart : ShoppingCartType.Wishlist;
        var canTransfer = !request.IsGuestCart && await permissions.AuthorizeAsync(target == ShoppingCartType.Wishlist ? StorefrontPermissions.Wishlist : StorefrontPermissions.Cart, request.Customer) &&
            await permissions.AuthorizeAsync(target == ShoppingCartType.Wishlist ? Permissions.Cart.AccessWishlist : Permissions.Cart.AccessShoppingCart, request.Customer);
        var lines = new List<CartLineDto>(nodes.Count);
        foreach (var node in nodes)
        {
            var item = node.Item;
            var root = item.ParentItemId == null;
            if (!products.TryGetValue(item.ProductId, out var product) || item.Product == null)
            {
                lines.Add(new(item.Id, item.ProductId, item.Quantity, node.Active, item.ParentItemId,
                    null, null, null, null, null, [], null, "unavailable", [], null,
                    new(false, root, false), ["product_unavailable"]));
                continue;
            }

            if (!urls.ContainsKey(product.Id))
                urls[product.Id] = pictureIds.TryGetValue(product.Id, out var picture)
                    ? await media.GetUrlAsync(picture, 400, doFallback: false) : null;
            var selected = new List<CartVariantDto>();
            var selectedAttributes = await attributes.MaterializeProductVariantAttributesAsync(item.AttributeSelection);
            var selectedValues = await attributes.MaterializeProductVariantAttributeValuesAsync(item.AttributeSelection);
            foreach (var attribute in selectedAttributes.Where(x => x.ProductId == item.ProductId && ProductSelection.SupportsControl(x.AttributeControlType)))
                selected.AddRange(selectedValues.Where(x => x.ProductVariantAttributeId == attribute.Id)
                    .Select(x => new CartVariantDto(attribute.Id, attribute.ProductAttribute?.Name, x.Id, x.Name)));

            var quantities = product.ParseAllowedQuantities();
            var min = Math.Max(1, quantities.Length > 0 ? quantities[0] : product.OrderMinimumQuantity);
            var max = Math.Max(min, quantities.Length > 0 ? quantities[^1] : product.OrderMaximumQuantity);
            var ctx = new AddToCartContext { Product = item.Product, Customer = request.Customer, StoreId = request.StoreId,
                CartType = type, Quantity = item.Quantity, RawAttributes = item.RawAttributes,
                BundleItem = item.BundleItem, ChildItems = node.ChildItems.Select(x => x.Item).ToList() };
            var valid = await validator.ValidateAddToCartItemAsync(ctx, item, native.Items);
            var availability = product.CallForPrice ? "price_on_request" : valid ? "available" : "validation_failed";
            lines.Add(new(item.Id, item.ProductId, item.Quantity, node.Active, item.ParentItemId,
                product.Name, product.Sku, urls[product.Id], min, Math.Min(1000, max),
                quantities.Where(q => q > 0 && q <= 1000).Distinct().ToArray(), Math.Max(1, product.QuantityStep),
                availability, selected.OrderBy(x => x.VariantAttributeId).ThenBy(x => x.ValueId).ToArray(), null,
                new(root && min <= 1000, root, root && valid && canTransfer),
                valid ? Array.Empty<string>() : canPrice ? ctx.Warnings.Distinct().ToArray() : new[] { "cart_validation_failed" }));
        }
        var rootActivity = lines.Where(x => x.ParentItemId == null).ToDictionary(x => x.Id, x => x.Active);
        lines = lines.Select(x => x.ParentItemId.HasValue && rootActivity.TryGetValue(x.ParentItemId.Value, out var active)
            ? x with { Active = active } : x).ToList();
        return (native, lines, canPrice);
    }

    private static IEnumerable<OrganizedShoppingCartItem> Flatten(OrganizedShoppingCartItem item)
    {
        yield return item;
        foreach (var child in item.ChildItems.SelectMany(Flatten)) yield return child;
    }

    private List<CartLineDto> WithPrices(List<CartLineDto> lines, ShoppingCartSubtotal subtotal)
    {
        if (subtotal?.LineItems == null) return lines;
        var lookup = subtotal.LineItems.ToDictionary(x => x.Item.Item.Id);
        var incl = work.TaxDisplayType == TaxDisplayType.IncludingTax;
        return lines.Select(x => lookup.TryGetValue(x.Id, out var price) ? x with
        {
            Pricing = new CartLinePricingDto(price.UnitPrice.FinalPrice.Currency.CurrencyCode,
                price.UnitPrice.FinalPrice.Amount, price.UnitPrice.RegularPrice?.Amount,
                price.Subtotal.FinalPrice.Amount, price.Subtotal.DiscountAmount.Amount, incl)
        } : x).ToList();
    }

    private static string MaskCode(string code) => string.IsNullOrEmpty(code) || code.Length <= 4
        ? "****" : "****" + code[^4..];

    private static string Revision<T>(ShoppingCart native, T state) => CartSnapshotRevision.Create(new
    {
        State = state,
        Rows = native.Items.SelectMany(Flatten).OrderBy(x => x.Item.Id).Select(x => new
        {
            x.Item.Id, x.Item.Quantity, x.Item.Active, x.Item.ParentItemId, x.Item.RawAttributes, x.Item.UpdatedOnUtc
        }).ToArray()
    });

    public async Task<List<CartItemDto>> ItemsAsync(ShoppingCartType type = ShoppingCartType.ShoppingCart)
    {
        var native = await cart.GetCartAsync(request.Customer, type, request.StoreId, activeOnly: null);
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

    public async Task<bool> SetRewardPointsAsync(bool use, CancellationToken ct)
    {
        if (use && !rewards.Enabled) return false;
        request.Customer.GenericAttributes.UseRewardPointsDuringCheckout = use;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CartTotalDto> TotalAsync()
    {
        var current = await cart.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId);
        var result = await calculations.GetShoppingCartTotalAsync(current, ShoppingCartTotalOptions.Default);
        return new CartTotalDto(result.Total?.Amount, result.DiscountAmount.Amount, result.DiscountAmount.Currency.CurrencyCode);
    }

    public async Task<bool> ApplyGiftCardAsync(string code, CancellationToken ct)
    {
        if (!settings.ShowGiftCardBox || string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        var current = await cart.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId);
        if (current.IncludesMatchingItems(x => x.IsRecurring)) return false;
        // Same lock namespace as MVC, so both entry points coordinate.
        var keyLock = locks.GetLock($"shoppingcart.applygiftcardcoupon:{code}");
        if (await keyLock.IsHeldAsync()) return false;
        using (await keyLock.AcquireAsync())
        {
            var card = await db.GiftCards.Include(x => x.GiftCardUsageHistory)
                .Include(x => x.PurchasedWithOrderItem).ThenInclude(x => x.Order)
                .AsSplitQuery().AsNoTracking().ApplyCouponFilter([code]).ApplyStandardFilter().FirstOrDefaultAsync(ct);
            if (card == null || !await gifts.ValidateGiftCardAsync(card, request.Customer, request.StoreId)) return false;
            var codes = new List<GiftCardCouponCode>(request.Customer.GenericAttributes.GiftCardCouponCodes);
            if (!codes.Any(x => x.Value.Equals(card.GiftCardCouponCode, StringComparison.OrdinalIgnoreCase)))
            {
                codes.Add(new GiftCardCouponCode(card.GiftCardCouponCode));
                request.Customer.GenericAttributes.GiftCardCouponCodes = codes;
                await db.SaveChangesAsync(ct);
            }
            return true;
        }
    }

    public async Task<bool> RemoveGiftCardAsync(int id, CancellationToken ct)
    {
        var card = await db.GiftCards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (card == null) return false;
        var codes = new List<GiftCardCouponCode>(request.Customer.GenericAttributes.GiftCardCouponCodes);
        var removed = codes.RemoveAll(x => x.Value.Equals(card.GiftCardCouponCode, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;
        request.Customer.GenericAttributes.GiftCardCouponCodes = codes;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ClearGiftCardsAsync(CancellationToken ct)
    {
        request.Customer.GenericAttributes.GiftCardCouponCodes = new List<GiftCardCouponCode>();
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ApplyCouponAsync(string code, CancellationToken ct)
    {
        if (request.IsGuestCart && !await CanDisplayPriceAsync()) return false;
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
