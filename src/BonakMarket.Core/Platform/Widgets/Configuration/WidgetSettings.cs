using BonakMarket.Core.Configuration;

namespace BonakMarket.Core.Widgets;

public class WidgetSettings : ISettings
{
    /// <summary>
    /// Gets or sets a system names of active widgets.
    /// </summary>
    public List<string> ActiveWidgetSystemNames { get; set; } = new();
}