using BonakMarket.Collections;
using BonakMarket.Core.Content.Menus;
using BonakMarket.Web.Rendering.Builders;

namespace BonakMarket.StorefrontApi;

public sealed class AdminMenu : IMenuProvider
{
    public string MenuName => "admin";
    public int Ordinal => 0;

    public void BuildMenu(TreeNode<MenuItem> rootNode)
    {
        rootNode.SelectNodeById("settings")?.Append(new MenuItem().ToBuilder()
            .Text("Storefront API").Icon("list").PermissionNames("configuration.setting")
            .Action("Configure", "StorefrontApiAdmin", new { area = "Admin" }).AsItem());
    }
}
