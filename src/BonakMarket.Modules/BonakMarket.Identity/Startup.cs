using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using BonakMarket.Identity.Security;
using BonakMarket.Identity.Swagger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Data;
using BonakMarket.Core.Messaging;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Widgets;
using BonakMarket.Data;
using BonakMarket.Data.Providers;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.Identity.Domain;
using BonakMarket.Identity.Filters;
using BonakMarket.Identity.Services;
using BonakMarket.Web.Controllers;

namespace BonakMarket.Identity;

/// <summary>
/// Registers native Customer authentication, OTP state and account APIs.
/// </summary>
internal sealed class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddIdentitySwagger();
        services.AddScoped<SmsOtpLoginService>();
        services.AddScoped<IMobileApiTokenService, MobileApiTokenService>();
        services.AddScoped<CustomerProfileService>();
        services.AddScoped<IStorefrontSmsProfileService>(sp => sp.GetRequiredService<CustomerProfileService>());
        services.AddScoped<ISmsOtpLoginService>(services => services.GetRequiredService<SmsOtpLoginService>());
        if (appContext.IsInstalled)
        {
            services.AddScoped<IWidgetSource, SmsLoginWidgetSource>();
        }
        services.Configure<MvcOptions>(options => options.Filters
            .AddEndpointFilter<SmsPrimaryLoginFilter, PublicController>()
            .ForController("Identity")
            .ForAction("Login"));
        services.AddTransient<IDbContextConfigurationSource<SmartDbContext>, IdentityModelConfigurer>();
    }

    public override void BuildPipeline(RequestPipelineBuilder builder)
    {
        if (!builder.ApplicationContext.IsInstalled) return;

        builder.Configure(StarterOrdering.BeforeRoutingMiddleware, app =>
            app.UseIdentitySwagger(builder.ApplicationContext.HostEnvironment, builder.ApplicationContext.ModuleCatalog));

        builder.Configure(StarterOrdering.BeforeWorkContextMiddleware, app =>
            app.UseMiddleware<CustomerApiBearerMiddleware>());
    }

    private sealed class IdentityModelConfigurer : IDbContextConfigurationSource<SmartDbContext>
    {
        public void Configure(IServiceProvider services, DbContextOptionsBuilder builder)
        {
            builder.UseDbFactory(factory => factory.AddModelAssembly(typeof(SmsLoginPhone).Assembly));
        }
    }
}
