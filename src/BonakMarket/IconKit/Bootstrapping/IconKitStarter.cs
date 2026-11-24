using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;

using BonakMarket.IconKit;
using BonakMarket.IO;

namespace BonakMarket.Bootstrapping;

/// <summary>
/// Integrates IconKit with BonakMarket's application data directory and endpoint pipeline.
/// </summary>
internal class IconKitStarter : StarterBase
{
    /// <inheritdoc />
    public override int Order => StarterOrdering.Early;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddIconKit(options =>
        {
            options.SourcePath = PathUtility.Combine(appContext.AppDataRoot.Root, "Icons");
            options.CachePath = PathUtility.Combine(appContext.AppDataRoot.Root, ".cache", "icons");
        });
    }

    /// <inheritdoc />
    public override void MapRoutes(EndpointRoutingBuilder builder)
    {
        builder.MapRoutes(StarterOrdering.EarlyRoute, routes => routes.MapIconKit());
    }
}
