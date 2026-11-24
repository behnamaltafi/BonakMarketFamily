using Autofac;
using BonakMarket.Core.DataExchange;
using BonakMarket.Core.DataExchange.Import;
using BonakMarket.Core.Messaging;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Core.Bootstrapping;

internal sealed class MessagingStarter : StarterBase
{
    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterType<EmailAccountService>().As<IEmailAccountService>().InstancePerLifetimeScope();
        builder.RegisterType<QueuedEmailService>().As<IQueuedEmailService>().InstancePerLifetimeScope();
        builder.RegisterType<QueuedEmailRateLimiter>().As<IEmailRateLimiter>().SingleInstance();

        builder.RegisterType<NewsletterSubscriptionService>().As<INewsletterSubscriptionService>().InstancePerLifetimeScope();
        builder.RegisterType<NewsletterSubscriptionImporter>().Keyed<IEntityImporter>(ImportEntityType.NewsletterSubscription).InstancePerLifetimeScope();

        builder.RegisterType<MessageFactory>().As<IMessageFactory>().InstancePerLifetimeScope();
        builder.RegisterType<MessageModelProvider>().As<IMessageModelProvider>().InstancePerLifetimeScope();
        builder.RegisterType<MessageModelHelper>().InstancePerLifetimeScope();
        builder.RegisterType<MessageTemplateService>().As<IMessageTemplateService>().InstancePerLifetimeScope();
        builder.RegisterType<CampaignService>().As<ICampaignService>().InstancePerLifetimeScope();
    }
}