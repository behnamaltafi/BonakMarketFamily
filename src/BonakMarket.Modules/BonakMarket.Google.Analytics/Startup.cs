using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.OutputCache;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.Google.Analytics.Filters;
using BonakMarket.Google.Analytics.Services;
using BonakMarket.Web.Controllers;

namespace BonakMarket.Google.Analytics;

internal class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        if (appContext.IsInstalled)
        {
            services.AddScoped<GoogleAnalyticsScriptHelper>();

            services.Configure<MvcOptions>(o =>
            {
                o.Filters.AddEndpointFilter<CheckoutFilter, CheckoutController>()
                    .ForAction(x => x.Confirm())
                    .WhenNonAjax();
            });
        }
    }

    public override void BuildPipeline(RequestPipelineBuilder builder)
    {
        // OutputCache invalidation.
        var observer = builder.ApplicationBuilder.ApplicationServices.GetRequiredService<IOutputCacheInvalidationObserver>();

        observer.ObserveSettingProperty<GoogleAnalyticsSettings>(x => x.TrackingScript, x => x.RemoveAllAsync());
        observer.ObserveSettingProperty<GoogleAnalyticsSettings>(x => x.DisplayCookieInfosForAds, x => x.RemoveAllAsync());
        observer.ObserveSettingProperty<GoogleAnalyticsSettings>(x => x.GoogleId, x => x.RemoveAllAsync());
        observer.ObserveSettingProperty<GoogleAnalyticsSettings>(x => x.MinifyScripts, x => x.RemoveAllAsync());
        observer.ObserveSettingProperty<GoogleAnalyticsSettings>(x => x.RenderWithUserConsentOnly, x => x.RemoveAllAsync());
        observer.ObserveSettingProperty<GoogleAnalyticsSettings>(x => x.RenderCatalogScripts, x => x.InvalidateByRouteAsync([.. OutputCacheDefaults.AllProductListsRoutes, OutputCacheDefaults.ProductDetailsRoute]));
    }
}