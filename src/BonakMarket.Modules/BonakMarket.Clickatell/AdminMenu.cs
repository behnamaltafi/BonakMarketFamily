using BonakMarket.Collections;
using BonakMarket.Core.Content.Menus;
using BonakMarket.Web.Rendering.Builders;

namespace BonakMarket.Clickatell;

public class AdminMenu : AdminMenuProvider
{
    protected override void BuildMenuCore(TreeNode<MenuItem> modulesNode)
    {
        var menuItem = new MenuItem().ToBuilder()
            .Text("Clickatell SMS Provider")
            .ResKey("Plugins.FriendlyName.BonakMarket.Clickatell")
            .Icon("send")
            .Action("Configure", "Clickatell", new { area = "Admin" })
            .AsItem();

        modulesNode.Append(menuItem);
    }
}