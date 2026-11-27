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

// Read and project the native cart; do not introduce a second cart engine.
internal sealed class CartStateReader(SmartDbContext db, IShoppingCartService cart, IOrderCalculationService calculations,
    IProductAttributeMaterializer attributes, StorefrontRequestContext request, IWorkContext work,
    IGiftCardService gifts, IPermissionService permissions, IMediaService media, IShoppingCartValidator validator)
{
    public Task<bool> CanDisplayPriceAsync() => permissions.AuthorizeAsync(Permissions.Catalog.DisplayPrice, request.Customer);

    internal async Task<CartStateDto> StateAsync(CancellationToken ct, IReadOnlyList<string> operationWarnings = null)
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

    internal async Task<WishlistStateDto> WishlistStateAsync(CancellationToken ct, IReadOnlyList<string> operationWarnings = null)
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
        var pictureIds = await CatalogMedia.MainPictureIdsAsync(db, productIds, ct);
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
                product.Name, product.Sku, urls[product.Id], min, Math.Min(StorefrontLimits.MaxQuantity, max),
                quantities.Where(q => q > 0 && q <= StorefrontLimits.MaxQuantity).Distinct().ToArray(), Math.Max(1, product.QuantityStep),
                availability, selected.OrderBy(x => x.VariantAttributeId).ThenBy(x => x.ValueId).ToArray(), null,
                new(root && min <= StorefrontLimits.MaxQuantity, root, root && valid && canTransfer),
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

    internal async Task<List<CartItemDto>> ItemsAsync(ShoppingCartType type = ShoppingCartType.ShoppingCart)
    {
        var native = await cart.GetCartAsync(request.Customer, type, request.StoreId, activeOnly: null);
        return native.Items.SelectMany(Flatten).Select(node =>
        {
            var x = node.Item;
            return new CartItemDto(x.Id, x.ProductId, x.Quantity, x.Active, x.ParentItemId);
        }).ToList();
    }

    public async Task<CartTotalDto> TotalAsync()
    {
        var current = await cart.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId);
        var result = await calculations.GetShoppingCartTotalAsync(current, ShoppingCartTotalOptions.Default);
        return new CartTotalDto(result.Total?.Amount, result.DiscountAmount.Amount, result.DiscountAmount.Currency.CurrencyCode);
    }
}
