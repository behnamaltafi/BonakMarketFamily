using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.AmazonPay.Filters;
using BonakMarket.AmazonPay.Services;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.Web.Controllers;

namespace BonakMarket.AmazonPay;

internal class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddScoped<IAmazonPayService, AmazonPayService>();

        services.Configure<AuthenticationOptions>((options) =>
        {
            if (!options.Schemes.Any(x => x.Name == AmazonPaySignInProvider.SystemName))
            {
                options.AddScheme<AmazonPaySignInHandler>(AmazonPaySignInProvider.SystemName, null);
            }
        });

        services.Configure<MvcOptions>(o =>
        {
            o.Filters.AddEndpointFilter<CheckoutFilter, CheckoutController>().WhenNonAjax();
            o.Filters.AddEndpointFilter<OffCanvasShoppingCartFilter, SmartController>()
                .ForController("ShoppingCart")
                .ForAction(nameof(ShoppingCartController.OffCanvasShoppingCart));
        });
    }
}