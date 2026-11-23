using Autofac;
using BonakMarket.Core.Checkout.Affiliates.Rules;
using BonakMarket.Core.Checkout.Attributes;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.GiftCards;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Checkout.Payment.Rules;
using BonakMarket.Core.Checkout.Rules;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Checkout.Shipping.Rules;
using BonakMarket.Core.Checkout.Tax;
using BonakMarket.Core.Identity.Rules;
using BonakMarket.Core.Rules;
using BonakMarket.Core.Rules.Rendering;
using BonakMarket.Engine.Builders;
using BonakMarket.Net.Http;

namespace BonakMarket.Core.Bootstrapping;

internal sealed class CheckoutStarter : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddHttpClient<ViesTaxationHttpClient>()
            .AddBonakMarketUserAgent()
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });
    }

    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterType<CheckoutAttributeMaterializer>().As<ICheckoutAttributeMaterializer>().InstancePerLifetimeScope();
        builder.RegisterType<CheckoutAttributeFormatter>().As<ICheckoutAttributeFormatter>().InstancePerLifetimeScope();
        builder.RegisterType<DefaultCheckoutStateAccessor>().As<ICheckoutStateAccessor>().InstancePerLifetimeScope();
        builder.RegisterType<OrderCalculationService>().As<IOrderCalculationService>().InstancePerLifetimeScope();
        builder.RegisterType<OrderProcessingService>().As<IOrderProcessingService>().InstancePerLifetimeScope();
        builder.RegisterType<ShoppingCartValidator>().As<IShoppingCartValidator>().InstancePerLifetimeScope();
        builder.RegisterType<ShoppingCartService>().As<IShoppingCartService>().InstancePerLifetimeScope();
        builder.RegisterType<GiftCardService>().As<IGiftCardService>().InstancePerLifetimeScope();
        builder.RegisterType<ShippingService>().As<IShippingService>().InstancePerLifetimeScope();
        builder.RegisterType<PaymentService>().As<IPaymentService>().InstancePerLifetimeScope();
        builder.RegisterType<OrderService>().As<IOrderService>().InstancePerLifetimeScope();
        builder.RegisterType<TaxService>().As<ITaxService>().InstancePerLifetimeScope();
        builder.RegisterType<TaxCalculator>().As<ITaxCalculator>().InstancePerLifetimeScope();

        // Checkout
        builder.RegisterType<CheckoutFactory>().As<ICheckoutFactory>().InstancePerLifetimeScope();
        builder.RegisterType<CheckoutWorkflow>().As<ICheckoutWorkflow>().InstancePerLifetimeScope();
        DiscoverCheckoutHandlers(builder, appContext);

        // Cart rules.
        var cartRuleTypes = appContext.TypeScanner.FindTypes<IRule<CartRuleContext>>().ToList();
        foreach (var ruleType in cartRuleTypes)
        {
            builder.RegisterType(ruleType).Keyed<IRule<CartRuleContext>>(ruleType).InstancePerLifetimeScope();
        }

        builder.RegisterType<CartRuleProvider>().As<ICartRuleProvider>().InstancePerLifetimeScope();

        // Rule options provider.
        builder.RegisterType<PaymentMethodRuleOptionsProvider>().As<IRuleOptionsProvider>().InstancePerLifetimeScope();
        builder.RegisterType<AuthenticationMethodRuleOptionsProvider>().As<IRuleOptionsProvider>().InstancePerLifetimeScope();
        builder.RegisterType<ShippingRateComputationMethodRuleOptionsProvider>().As<IRuleOptionsProvider>().InstancePerLifetimeScope();
        builder.RegisterType<ShippingMethodRuleOptionsProvider>().As<IRuleOptionsProvider>().InstancePerLifetimeScope();
        builder.RegisterType<AffiliateRuleOptionsProvider>().As<IRuleOptionsProvider>().InstancePerLifetimeScope();
    }

    private static void DiscoverCheckoutHandlers(ContainerBuilder builder, IApplicationContext appContext)
    {
        var handlerTypes = appContext.TypeScanner.FindTypes<ICheckoutHandler>();

        foreach (var handlerType in handlerTypes)
        {
            var targetAttribute = handlerType.GetAttribute<CheckoutStepAttribute>(true);

            builder
                .RegisterType(handlerType)
                .As<ICheckoutHandler>()
                .Keyed<ICheckoutHandler>(handlerType)
                .InstancePerAttributedLifetime()
                .WithMetadata<CheckoutHandlerMetadata>(m =>
                {
                    m.For(x => x.HandlerType, handlerType);
                    m.For(x => x.Actions, targetAttribute?.Actions);
                    m.For(x => x.Controller, targetAttribute?.Controller ?? "Checkout");
                    m.For(x => x.Area, targetAttribute?.Area);
                    m.For(x => x.Order, targetAttribute?.Order ?? 0);
                    m.For(x => x.ProgressLabelKey, targetAttribute?.ProgressLabelKey);
                });
        }

        builder.Register<Func<Type, ICheckoutHandler>>(c =>
        {
            var cc = c.Resolve<IComponentContext>();
            return key => cc.ResolveKeyed<ICheckoutHandler>(key);
        });
    }
}