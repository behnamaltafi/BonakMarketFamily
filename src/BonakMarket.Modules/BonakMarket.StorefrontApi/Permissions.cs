using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;

namespace BonakMarket.StorefrontApi;

public static class StorefrontPermissions
{
    public const string Self = "storefront";
    public const string Catalog = "storefront.catalog";
    public const string Cart = "storefront.cart";
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

public sealed class StorefrontPermissionProvider : IPermissionProvider
{
    public IEnumerable<PermissionRecord> GetPermissions() =>
        PermissionHelper.GetPermissions(typeof(StorefrontPermissions)).Select(x => new PermissionRecord { SystemName = x });

    public IEnumerable<DefaultPermissionRecord> GetDefaultPermissions()
    {
        yield return For(SystemCustomerRoleNames.Guests, StorefrontPermissions.Catalog);
        yield return For(SystemCustomerRoleNames.Registered, StorefrontPermissions.Catalog,
            StorefrontPermissions.Cart, StorefrontPermissions.Orders, StorefrontPermissions.Wishlist,
            StorefrontPermissions.RewardPoints, StorefrontPermissions.StockSubscriptions, StorefrontPermissions.Checkout,
            StorefrontPermissions.Returns, StorefrontPermissions.Downloads, StorefrontPermissions.Reviews, StorefrontPermissions.Sharing);
        yield return For(SystemCustomerRoleNames.Administrators, StorefrontPermissions.Self);
    }

    private static DefaultPermissionRecord For(string role, params string[] permissions) => new()
    {
        CustomerRoleSystemName = role,
        PermissionRecords = permissions.Select(x => new PermissionRecord { SystemName = x }).ToArray()
    };
}
