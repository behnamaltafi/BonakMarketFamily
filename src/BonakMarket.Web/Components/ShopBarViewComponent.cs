using BonakMarket.Core.Catalog;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.Web.Models.Common;

namespace BonakMarket.Web.Components;

public class ShopBarViewComponent : SmartViewComponent
{
    private readonly CustomerSettings _customerSettings;
    private readonly CatalogSettings _catalogSettings;
    private readonly ShoppingCartSettings _shoppingCartSettings;

    public ShopBarViewComponent(CustomerSettings customerSettings, CatalogSettings catalogSettings, ShoppingCartSettings shoppingCartSettings)
    {
        _customerSettings = customerSettings;
        _catalogSettings = catalogSettings;
        _shoppingCartSettings = shoppingCartSettings;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var customer = Services.WorkContext.CurrentCustomer;
        var isAdmin = customer.IsAdmin();
        var isRegistered = isAdmin || customer.IsRegistered();

        var model = new ShopBarModel
        {
            IsAuthenticated = isRegistered,
            CustomerEmailUsername = isRegistered ? (_customerSettings.SupportsUsernames() ? customer.Username : customer.Email) : string.Empty,
            IsCustomerImpersonated = Services.WorkContext.CurrentImpersonator != null,
            DisplayAdminLink = await Services.Permissions.AuthorizeAsync(Permissions.System.AccessBackend),
            ShoppingCartEnabled = await Services.Permissions.AuthorizeAsync(Permissions.Cart.AccessShoppingCart) && _shoppingCartSettings.MiniShoppingCartEnabled,
            WishlistEnabled = await Services.Permissions.AuthorizeAsync(Permissions.Cart.AccessWishlist),
            CompareProductsEnabled = _catalogSettings.CompareProductsEnabled,
            PublicStoreNavigationAllowed = await Services.Permissions.AuthorizeAsync(Permissions.System.AccessShop)
        };

        return View(model);
    }
}