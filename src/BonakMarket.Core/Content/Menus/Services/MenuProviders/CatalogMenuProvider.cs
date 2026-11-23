using BonakMarket.Collections;

namespace BonakMarket.Core.Content.Menus;

public abstract class CatalogMenuProvider : IMenuProvider
{
    public abstract void BuildMenu(TreeNode<MenuItem> rootNode);

    public string MenuName => "catalog";

    public virtual int Ordinal => 0;
}