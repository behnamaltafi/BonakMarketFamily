using Autofac;
using Microsoft.AspNetCore.Http;
using BonakMarket.Core.Common.Rules;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Rules.Rendering;
using BonakMarket.Core.Web;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Core.Bootstrapping;

internal sealed class CommonStarter : StarterBase
{
    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterType<GeoCountryLookup>().As<IGeoCountryLookup>().SingleInstance();
        builder.RegisterType<CurrencyService>().As<ICurrencyService>().InstancePerLifetimeScope();
        builder.RegisterType<RoundingHelper>().As<IRoundingHelper>().InstancePerLifetimeScope();
        builder.RegisterType<AddressService>().As<IAddressService>().InstancePerLifetimeScope();
        builder.RegisterType<DateTimeHelper>().As<IDateTimeHelper>().InstancePerLifetimeScope();
        builder.RegisterType<DeliveryTimeService>().As<IDeliveryTimeService>().InstancePerLifetimeScope();
        builder.RegisterType<MeasureService>().As<IMeasureService>().InstancePerLifetimeScope();
        builder.RegisterType<GenericAttributeService>().As<IGenericAttributeService>().InstancePerLifetimeScope();
        builder.RegisterType<DefaultWebHelper>().As<IWebHelper>().InstancePerLifetimeScope();
        builder.RegisterType<PreviewModeCookie>().As<IPreviewModeCookie>().InstancePerLifetimeScope();
        builder.RegisterType<CollectionGroupService>().As<ICollectionGroupService>().InstancePerLifetimeScope();

        // Rule options provider.
        builder.RegisterType<CommonRuleOptionsProvider>().As<IRuleOptionsProvider>().InstancePerLifetimeScope();

        // User agent
        builder.RegisterType<DefaultUserAgentParser>().As<IUserAgentParser>().SingleInstance();
        builder.RegisterType<DefaultUserAgentFactory>().As<IUserAgentFactory>().SingleInstance();
        builder.Register<IUserAgent>(c =>
        {
            // Resolve factory
            var factory = c.Resolve<IUserAgentFactory>();
            // Resolve user agent string from current HttpContext
            var userAgent = c.ResolveOptional<IHttpContextAccessor>()?.HttpContext?.Request?.UserAgent() ?? string.Empty;
            // Create user agent
            return factory.CreateUserAgent(userAgent);
        }).InstancePerLifetimeScope();
    }
}