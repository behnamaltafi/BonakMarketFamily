using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class CustomerStorefrontService(SmartDbContext db, StorefrontRequestContext request,
    IPermissionService permissions, IShoppingCartService carts, RewardPointsSettings rewards,
    CustomerSettings settings)
{
    private Task<bool> Allowed(string permission) => permissions.AuthorizeAsync(permission, request.Customer);

    public async Task<CustomerSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        int? cart = null, wishlist = null, orders = null, returns = null, stock = null, points = null;
        if (await Allowed(StorefrontPermissions.Cart) && await Allowed(Permissions.Cart.AccessShoppingCart))
            cart = await carts.CountProductsInCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId, activeOnly: null);
        if (await Allowed(StorefrontPermissions.Wishlist) && await Allowed(Permissions.Cart.AccessWishlist))
            wishlist = await carts.CountProductsInCartAsync(request.Customer, ShoppingCartType.Wishlist, request.StoreId, activeOnly: null);
        if (await Allowed(StorefrontPermissions.Orders))
        {
            orders = await StorefrontScope.Orders(db, request.Customer, request.StoreId).CountAsync(ct);
            if (await Allowed(StorefrontPermissions.Returns))
                returns = await db.ReturnCases.CountAsync(x => x.CustomerId == request.Customer.Id && x.StoreId == request.StoreId, ct);
        }
        if (!settings.HideBackInStockSubscriptionsTab && await Allowed(StorefrontPermissions.StockSubscriptions))
            stock = await db.BackInStockSubscriptions.CountAsync(x => x.CustomerId == request.Customer.Id && x.StoreId == request.StoreId, ct);
        if (rewards.Enabled && await Allowed(StorefrontPermissions.RewardPoints))
            points = await db.RewardPointsHistory.AsNoTracking().ApplyCustomerFilter([request.Customer.Id])
                .Select(x => (int?)x.PointsBalance).FirstOrDefaultAsync(ct) ?? 0;
        return new CustomerSummaryDto(cart, wishlist, orders, returns, stock, points,
            await CustomerGuildPolicy.RequiresGuildAsync(db, request.Customer, ct));
    }

    public async Task<PageDto<MyReviewDto>> GetReviewsAsync(PageRequest page, CancellationToken ct)
    {
        // Product reviews are global native records, not store-specific; only show current-store visible products.
        var visible = StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true);
        var query = db.ProductReviews.AsNoTracking().Where(x => x.CustomerId == request.Customer.Id &&
            visible.Any(p => p.Id == x.ProductId));
        return await query.OrderByDescending(x => x.CreatedOnUtc).ThenByDescending(x => x.Id)
            .Select(x => new MyReviewDto(x.Id, x.ProductId, x.Product.Name, x.Title, x.ReviewText,
                x.Rating, x.IsApproved, x.CreatedOnUtc)).PageAsync(page, ct);
    }
}
