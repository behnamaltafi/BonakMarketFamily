using Autofac;
using BonakMarket.Core.Rules;
using BonakMarket.Core.Rules.Rendering;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Core.Bootstrapping;

internal class RuleStarter : StarterBase
{
    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterType<RuleService>().As<IRuleService>().InstancePerLifetimeScope();
        builder.RegisterType<RuleProviderFactory>().As<IRuleProviderFactory>().InstancePerLifetimeScope();
        builder.RegisterType<RuleSetRuleOptionsProvider>().As<IRuleOptionsProvider>().InstancePerLifetimeScope();

        // Rendering.
        builder.RegisterType<RuleTemplateSelector>().As<IRuleTemplateSelector>().InstancePerLifetimeScope();
    }
}