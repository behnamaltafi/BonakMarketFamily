using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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
        services.AddCors();
        services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<Microsoft.Extensions.Configuration.IConfiguration>((options, configuration) =>
            {
                var origins = configuration.GetSection("Identity:AllowedOrigins").GetChildren()
                    .Select(x => x.Value)
                    .Where(x => Uri.TryCreate(x, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
                        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) &&
                        string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/" &&
                        !uri.Host.Contains('*'))
                    .Select(x => new Uri(x).GetLeftPart(UriPartial.Authority))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                options.AddPolicy("CustomerApiCorsPolicy", policy =>
                {
                    if (origins.Length != 0) policy.WithOrigins(origins);
                    policy.AllowAnyHeader()
                        .WithMethods("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                        .WithExposedHeaders("Retry-After", "WWW-Authenticate", "Content-Disposition");
                    // No AllowCredentials: customer auth is Bearer, never an ambient cookie.
                });
            });
        services.Configure<MvcOptions>(options => options.Filters.Add(new CustomerApiProblemFilter()));
        services.AddScoped<IUserValidator<Customer>, PhoneUsernameValidator>();
        if (appContext.IsInstalled)
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, VerifiedPhoneCookie.Configure);
        services.AddScoped<SmsOtpLoginService>();
        services.AddScoped<MobileApiTokenService>();
        services.AddScoped<IMobileApiTokenService>(sp => sp.GetRequiredService<MobileApiTokenService>());
        services.AddScoped<ICustomerSessionRevoker>(sp => sp.GetRequiredService<MobileApiTokenService>());
        services.AddScoped<CustomerProfileService>();
        services.AddScoped<CustomerAddressService>();
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

        // Handle API preflight before the native cookie/authentication pipeline;
        // the policy is scoped to customer API paths and does not alter OData.
        builder.Configure(StarterOrdering.RoutingMiddleware + 1, app =>
            app.UseWhen(ctx => CustomerApiProblemFilter.IsCanonicalPath(ctx.Request.Path),
                customer => customer.UseCors("CustomerApiCorsPolicy")));

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
