using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Domain;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class GuestCartService(SmartDbContext db, ICustomerService customers, IShoppingCartService native,
    ICurrencyService currencies, StorefrontRequestContext request, IConfiguration config, CartFacade cart)
{
    public async Task<GuestCartCreatedDto> CreateAsync(CancellationToken ct)
    {
        if (db.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) != true) return null;
        var raw = GuestCartPolicy.Generate();
        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // No ClientIdent or visitor cookie: never claim a heuristic/native visitor's existing cart.
        var customer = await customers.CreateGuestCustomerAsync();
        await db.Entry(customer).Collection(x => x.CustomerRoleMappings).Query().Include(x => x.CustomerRole).LoadAsync(ct);
        if (!GuestCartPolicy.IsGuest(customer)) throw new InvalidOperationException("Guest role unavailable.");
        var token = new GuestCartToken { TokenHash = GuestCartPolicy.Hash(raw), CustomerId = customer.Id,
            StoreId = request.StoreId, CreatedOnUtc = now, ExpiresOnUtc = now.AddDays(GuestCartPolicy.ExpirationDays(config)) };
        db.Set<GuestCartToken>().Add(token);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(raw, token.ExpiresOnUtc);
    }

    public async Task<GuestCartMergeResult> MergeAsync(string hash, CancellationToken ct)
    {
        if (hash == null || !request.Customer.IsRegistered()) return new(401, "authentication_required");
        // Middleware holds destination and source SQL locks. Receipt, copies and deletion commit together.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var token = await db.Set<GuestCartToken>().FirstOrDefaultAsync(x => x.TokenHash == hash && x.StoreId == request.StoreId, ct);
        if (token == null || token.ExpiresOnUtc <= DateTime.UtcNow) return new(401, "invalid_guest_cart_token");
        if (token.MergedToCustomerId == request.Customer.Id && token.MergedOnUtc.HasValue)
        {
            await tx.CommitAsync(ct);
            return new(200, Warning: token.MergeWarning);
        }
        if (token.RevokedOnUtc.HasValue || token.MergedOnUtc.HasValue) return new(401, "invalid_guest_cart_token");
        var source = await db.Customers.IncludeCustomerRoles().FirstOrDefaultAsync(x => x.Id == token.CustomerId, ct);
        if (!GuestCartPolicy.IsGuest(source) || source.Id == request.Customer.Id)
            return new(401, "invalid_guest_cart_token");

        var sourceCart = await native.GetCartAsync(source, ShoppingCartType.ShoppingCart, request.StoreId, activeOnly: null);
        var roots = sourceCart.Items.OrderBy(x => x.Item.Id).ToArray();
        var sourceRows = roots.SelectMany(x => new[] { x.Item }.Concat(x.ChildItems.Select(y => y.Item))).ToArray();
        // Native organization skips orphan children. Never consume the token while leaving rows behind.
        var allRows = await db.ShoppingCartItems.Where(x => x.CustomerId == source.Id && x.StoreId == request.StoreId &&
            x.ShoppingCartTypeId == (int)ShoppingCartType.ShoppingCart).Select(x => new { x.Id, x.ProductId }).ToListAsync(ct);
        var organizedIds = sourceRows.Select(x => x.Id).ToHashSet();
        var orphans = allRows.Where(x => !organizedIds.Contains(x.Id))
            .Select(x => new GuestCartConflictDto(x.Id, x.ProductId, "cart_item_requires_resolution")).ToArray();
        if (orphans.Length != 0) return new(409, "guest_cart_merge_conflict", orphans);
        var guestCoupon = source.GenericAttributes.DiscountCouponCode;
        var customerCoupon = request.Customer.GenericAttributes.DiscountCouponCode;
        var sourceIds = sourceRows.Select(x => x.ProductId).Distinct().ToArray();
        var visible = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .Where(x => sourceIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        var conflicts = new List<GuestCartConflictDto>();
        foreach (var root in roots)
        {
            // CopyAsync cannot preserve inactive selections. Do not silently activate them.
            if (!root.Active || root.ChildItems.Any(x => !x.Active))
                conflicts.Add(new(root.Item.Id, root.Item.ProductId, "inactive_item_requires_resolution"));
            else if (root.Item.Product == null || !visible.Contains(root.Item.ProductId) || root.ChildItems.Any(x => x.Item.Product == null || !visible.Contains(x.Item.ProductId)))
                conflicts.Add(new(root.Item.Id, root.Item.ProductId, "product_unavailable"));
        }
        if (conflicts.Count > 0) return new(409, "guest_cart_merge_conflict", conflicts);
        foreach (var root in roots)
        {
            var item = root.Item;
            var ctx = new AddToCartContext
            {
                Customer = request.Customer, StoreId = request.StoreId, CartType = ShoppingCartType.ShoppingCart,
                Product = item.Product, Quantity = item.Quantity, RawAttributes = item.AttributeSelection.AsJson(),
                CustomerEnteredPrice = new(item.CustomerEnteredPrice, currencies.PrimaryCurrency),
                ChildItems = root.ChildItems.Select(x => x.Item).ToList()
            };
            if (!await native.CopyAsync(ctx))
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return new(409, "guest_cart_merge_conflict",
                    new[] { new GuestCartConflictDto(item.Id, item.ProductId, "cart_validation_failed") });
            }
        }

        // Native writes may reset checkout selections. Keep the customer's coupon; only reapply
        // the guest coupon when there is no customer coupon, and validate it under the registered identity.
        request.Customer.GenericAttributes.DiscountCouponCode = customerCoupon;
        if (!string.IsNullOrEmpty(guestCoupon) && !string.Equals(guestCoupon, customerCoupon, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(customerCoupon))
                token.MergeWarning = "guest_coupon_conflict";
            else if (!await cart.ApplyCouponAsync(guestCoupon, ct))
            {
                request.Customer.GenericAttributes.DiscountCouponCode = null;
                token.MergeWarning = "guest_coupon_not_applicable";
            }
        }

        // Delete only this store/cart, never wishlist or account financial data.
        if (sourceRows.Length > 0)
        {
            db.ShoppingCartItems.RemoveRange(sourceRows);
            request.Customer.GenericAttributes.Set<string>("BonakMarket.Storefront.CheckoutDraft.v1", null, request.StoreId);
        }
        token.MergedToCustomerId = request.Customer.Id;
        token.MergedOnUtc = token.RevokedOnUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(200, Warning: token.MergeWarning);
    }
}
public sealed record GuestCartMergeResult(int Status, string Code = null, IReadOnlyList<GuestCartConflictDto> Conflicts = null, string Warning = null);
