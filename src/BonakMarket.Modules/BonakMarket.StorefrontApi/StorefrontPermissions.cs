using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;

namespace BonakMarket.StorefrontApi;

public static class StorefrontPermissions
{
    public const string Self = "storefront";
    public const string Catalog = "storefront.catalog";
    public const string Cart = "storefront.cart";
    public const string GuestCart = "storefront.guestcart";
    public const string Orders = "storefront.orders";
    public const string Wishlist = "storefront.wishlist";
    public const string RewardPoints = "storefront.rewardpoints";
    public const string StockSubscriptions = "storefront.stocksubscriptions";
    public const string Checkout = "storefront.checkout";
    public const string Returns = "storefront.returns";
    public const string Downloads = "storefront.downloads";
    public const string Reviews = "storefront.reviews";
    public const string Sharing = "storefront.sharing";
}
