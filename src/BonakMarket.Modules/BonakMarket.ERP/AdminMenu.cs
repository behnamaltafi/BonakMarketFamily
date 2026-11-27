using BonakMarket.Collections;
using BonakMarket.Core.Content.Menus;
using BonakMarket.Web.Rendering.Builders;

namespace BonakMarket.ERP;
public sealed class AdminMenu : IMenuProvider
{
    public string MenuName => "admin";
    public int Ordinal => 0;
    public void BuildMenu(TreeNode<MenuItem> rootNode)
    {
        rootNode.SelectNodeById("settings")?.Append(new MenuItem().ToBuilder()
            .Text("ERP Integration").Icon("list").PermissionNames("configuration.setting")
            .Action("Index", "Erp", new { area = "Admin" }).AsItem());
    }
}
