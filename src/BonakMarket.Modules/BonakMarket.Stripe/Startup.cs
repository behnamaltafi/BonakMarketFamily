using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.StripeElements.Filters;
using BonakMarket.StripeElements.Services;
using BonakMarket.Web.Controllers;

namespace BonakMarket.StripeElements;

internal class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.Configure<MvcOptions>(o =>
        {
            o.Filters.AddEndpointFilter<OffCanvasShoppingCartFilter, SmartController>()
                .ForController("ShoppingCart")
                .ForAction(nameof(ShoppingCartController.OffCanvasShoppingCart));
            o.Filters.AddEndpointFilter<StripeScriptIncludeFilter, PublicController>().WhenNonAjax();
            o.Filters.AddEndpointFilter<CheckoutFilter, CheckoutController>(order: 200)
                .ForAction(x => x.PaymentMethod())
                .ForAction(x => x.Confirm())
                .WhenNonAjax();
        });

        if (appContext.IsInstalled)
        {
            services.AddScoped<StripeHelper>();
        }
    }
}