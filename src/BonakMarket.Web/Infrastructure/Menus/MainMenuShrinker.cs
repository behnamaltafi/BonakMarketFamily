using BonakMarket.Core.Catalog;
using BonakMarket.Core.Content.Menus;
using BonakMarket.Events;

namespace BonakMarket.Web.Infrastructure;

public class MainMenuShrinker : IConsumer
{
    public void HandleEvent(MenuBuiltEvent message,
        CatalogSettings catalogSettings)
    {
        if (message.Name != "Main" || catalogSettings.MaxItemsToDisplayInCatalogMenu.GetValueOrDefault() < 1)
        {
            return;
        }

        message.Root.Children
            .Where(x => x.Value.Id != "brand")
            .Skip(catalogSettings.MaxItemsToDisplayInCatalogMenu.Value)
            .Each(x => x.SetMetadata("spare", true));
    }
}