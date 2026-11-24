using System.Net;
using System.Net.Http;
using Autofac;
using BonakMarket.Core.AI;
using BonakMarket.Core.AI.Metadata;
using BonakMarket.Core.AI.Prompting;
using BonakMarket.Engine.Builders;
using BonakMarket.Net.Http;

namespace BonakMarket.Core.Bootstrapping;

internal sealed class AIStarter : StarterBase
{
    public override bool Matches(IApplicationContext appContext)
        => appContext.IsInstalled;

    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddHttpClient("AIRemoteMetadata")
            .AddBonakMarketUserAgent()
            .ConfigurePrimaryHttpMessageHandler(c => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip
            })
            .ConfigureHttpClient(client =>
            {
                //client.BaseAddress = new Uri("http://localhost:59318/");
                client.BaseAddress = new Uri("https://BonakMarket.com/");
                client.Timeout = TimeSpan.FromSeconds(2);
            });
    }

    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        // Register all prompt generators.
        var promptGeneratorTypes = appContext.TypeScanner.FindTypes<IAIPromptGenerator>();
        foreach (var type in promptGeneratorTypes)
        {
            builder.RegisterType(type).As<IAIPromptGenerator>().Keyed<IAIPromptGenerator>(type).InstancePerLifetimeScope();
        }

        builder.RegisterType<RemoteAIMetadataLoader>().As<IRemoteAIMetadataLoader>().SingleInstance();
        builder.RegisterType<DefaultAIMetadataLoader>().As<IAIMetadataLoader>().SingleInstance();
        builder.RegisterType<DefaultAIChatCache>().As<IAIChatCache>().SingleInstance();
        builder.RegisterType<AIMessageBuilder>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<AIMessageResources>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<AIProviderFactory>().As<IAIProviderFactory>().InstancePerLifetimeScope();
        builder.RegisterType<DefaultLlmsGenerator>().As<ILlmsGenerator>().InstancePerLifetimeScope();
    }
}