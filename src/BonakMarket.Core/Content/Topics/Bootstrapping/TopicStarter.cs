using Autofac;
using BonakMarket.Core.Content.Topics;
using BonakMarket.Core.Seo;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Core.Bootstrapping;

internal class TopicStarter : StarterBase
{
    public override bool Matches(IApplicationContext appContext)
        => appContext.IsInstalled;

    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterType<TopicXmlSitemapPublisher>().As<IXmlSitemapPublisher>().InstancePerLifetimeScope();
        builder.RegisterType<TopicWidgetSource>().As<IWidgetSource>().InstancePerLifetimeScope();
    }
}