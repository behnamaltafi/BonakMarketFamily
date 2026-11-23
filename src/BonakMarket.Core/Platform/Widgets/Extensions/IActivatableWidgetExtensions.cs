using BonakMarket.Engine.Modularity;

namespace BonakMarket.Core.Widgets;

public static class IActivatableWidgetExtensions
{
    public static bool IsWidgetActive(this Provider<IActivatableWidget> widget, WidgetSettings widgetSettings)
    {
        Guard.NotNull(widget);

        return widget.ToLazy().IsWidgetActive(widgetSettings);
    }

    public static bool IsWidgetActive(this Lazy<IActivatableWidget, ProviderMetadata> widget, WidgetSettings widgetSettings)
    {
        Guard.NotNull(widget);
        Guard.NotNull(widgetSettings);

        if (widgetSettings.ActiveWidgetSystemNames == null)
        {
            return false;
        }

        return widgetSettings.ActiveWidgetSystemNames.Contains(widget.Metadata.SystemName, StringComparer.OrdinalIgnoreCase);
    }
}