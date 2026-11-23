using BonakMarket.Core.OutputCache;

namespace BonakMarket.Google.Analytics;

internal sealed class CacheableRoutes : ICacheableRouteProvider
{
    public int Order => 0;

    public IEnumerable<string> GetCacheableRoutes() => ["vc:BonakMarket.Google.Analytics/GoogleAnalytics"];
}