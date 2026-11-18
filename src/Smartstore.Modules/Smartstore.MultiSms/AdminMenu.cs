using Smartstore.Collections;
using Smartstore.Core.Content.Menus;
using Smartstore.Web.Rendering.Builders;

namespace Smartstore.MultiSms;

public sealed class AdminMenu : AdminMenuProvider
{
    protected override void BuildMenuCore(TreeNode<MenuItem> modulesNode)
    {
        var item = new MenuItem().ToBuilder()
            .Text("SMS Gateway")
            .ResKey("Plugins.FriendlyName.Smartstore.MultiSms")
            .Icon("message-square")
            .Action("Configure", "MultiSms", new { area = "Admin" })
            .AsItem();

        modulesNode.Append(item);
    }
}
