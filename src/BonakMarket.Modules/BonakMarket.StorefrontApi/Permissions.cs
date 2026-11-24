using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;

namespace BonakMarket.StorefrontApi;

public static class StorefrontPermissions
{
    public const string Self = "storefront";
    public const string Catalog = "storefront.catalog";
    public const string Cart = "storefront.cart";
    public const string Orders = "storefront.orders";
}

public sealed class StorefrontPermissionProvider : IPermissionProvider
{
    public IEnumerable<PermissionRecord> GetPermissions() =>
        PermissionHelper.GetPermissions(typeof(StorefrontPermissions)).Select(x => new PermissionRecord { SystemName = x });

    public IEnumerable<DefaultPermissionRecord> GetDefaultPermissions()
    {
        yield return For(SystemCustomerRoleNames.Guests, StorefrontPermissions.Catalog);
        yield return For(SystemCustomerRoleNames.Registered, StorefrontPermissions.Catalog,
            StorefrontPermissions.Cart, StorefrontPermissions.Orders);
        yield return For(SystemCustomerRoleNames.Administrators, StorefrontPermissions.Self);
    }

    private static DefaultPermissionRecord For(string role, params string[] permissions) => new()
    {
        CustomerRoleSystemName = role,
        PermissionRecords = permissions.Select(x => new PermissionRecord { SystemName = x }).ToArray()
    };
}
