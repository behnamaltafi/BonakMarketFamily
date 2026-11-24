using BonakMarket.Caching;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Common.Configuration;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Data;
using BonakMarket.Data.Hooks;
using BonakMarket.Utilities;

namespace BonakMarket.Core.Catalog.Attributes;

[ServiceLifetime(ServiceLifetime.Singleton)]
internal class UnavailableAttributeCombinationsHook : AsyncDbSaveHook<BaseEntity>
{
    private readonly ICacheManager _cache;

    public UnavailableAttributeCombinationsHook(ICacheManager cache)
    {
        _cache = cache;
    }

    public override async Task<HookResult> OnAfterSaveAsync(IHookedEntity entry, CancellationToken cancelToken)
    {
        var entity = entry.Entity;
        var productId = 0;

        if (entity is Setting setting)
        {
            if (setting.Name.EqualsNoCase(TypeHelper.NameOf<PerformanceSettings>(x => x.MaxUnavailableAttributeCombinations, true)))
            {
                await _cache.RemoveByPatternAsync(ProductAttributeMaterializer.UanavailableCombinationsPatternKey);
            }
        }
        else if (entity is Product)
        {
            productId = entity.Id;
        }
        else if (entity is ProductVariantAttributeCombination combination)
        {
            productId = combination.ProductId;
        }
        else
        {
            return HookResult.Void;
        }

        if (productId != 0)
        {
            await _cache.RemoveAsync(ProductAttributeMaterializer.UnavailableCombinationsKey.FormatInvariant(productId));
        }

        return HookResult.Ok;
    }
}