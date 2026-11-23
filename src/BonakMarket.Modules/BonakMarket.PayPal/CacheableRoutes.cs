using BonakMarket.Core.OutputCache;

namespace BonakMarket.PayPal;

internal sealed class CacheableRoutes : ICacheableRouteProvider
{
    public int Order => 0;

    public IEnumerable<string> GetCacheableRoutes()
    {
        return ["vc:BonakMarket.PayPal/PayPalPayLaterMessage"];
    }
}