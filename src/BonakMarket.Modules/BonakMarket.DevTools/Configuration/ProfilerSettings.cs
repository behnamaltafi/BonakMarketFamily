using BonakMarket.Core.Configuration;

namespace BonakMarket.DevTools;

public class ProfilerSettings : ISettings
{
    public bool EnableMiniProfilerInPublicStore { get; set; }

    public string[] MiniProfilerIgnorePaths { get; set; } = new[] { "/admin/", "/themes/", "/bundle/", "/media/", "/components/", "/lib/", "/js/", "/css/", "/images/", "/taskscheduler/" };

    public bool DisplayWidgetZones { get; set; }
    public bool DisplayMachineName { get; set; }
}