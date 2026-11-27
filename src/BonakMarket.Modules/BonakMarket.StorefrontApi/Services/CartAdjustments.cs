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

// Apply discounts, gift cards and rewards through the existing native cart services and attributes.
public sealed class CartAdjustments(SmartDbContext db, IShoppingCartService cart, IOrderCalculationService calculations,
    StorefrontRequestContext request, ShoppingCartSettings settings, RewardPointsSettings rewards,
    IGiftCardService gifts, IDistributedLockProvider locks, IPermissionService permissions)
{
    private Task<bool> CanDisplayPriceAsync() => permissions.AuthorizeAsync(Permissions.Catalog.DisplayPrice, request.Customer);

    public async Task<bool> SetRewardPointsAsync(bool use, CancellationToken ct)
    {
        if (use && !rewards.Enabled) return false;
        request.Customer.GenericAttributes.UseRewardPointsDuringCheckout = use;
        await db.SaveChangesAsync(ct);
        return true;
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
