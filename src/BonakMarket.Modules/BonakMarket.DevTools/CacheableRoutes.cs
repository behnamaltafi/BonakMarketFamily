using BonakMarket.Core.OutputCache;

namespace BonakMarket.DevTools;

internal sealed class CacheableRoutes : ICacheableRouteProvider
{
    public int Order => 0;

    public IEnumerable<string> GetCacheableRoutes()
        => ["vc:BonakMarket.DevTools/CustomFacet"];
}