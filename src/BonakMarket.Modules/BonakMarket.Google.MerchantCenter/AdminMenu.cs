using BonakMarket.Collections;
using BonakMarket.Core.Content.Menus;
using BonakMarket.Web.Rendering.Builders;

namespace BonakMarket.Google.MerchantCenter;

public class AdminMenu : AdminMenuProvider
{
    protected override void BuildMenuCore(TreeNode<MenuItem> modulesNode)
    {
        var menuItem = new MenuItem().ToBuilder()
            .Text("Google Merchant Center")
            .ResKey("Plugins.FriendlyName.BonakMarket.Google.MerchantCenter")
            .Icon("google")
            .Action("Configure", "GoogleMerchantCenter", new { area = "Admin" })
            .AsItem();

        modulesNode.Append(menuItem);
    }
}