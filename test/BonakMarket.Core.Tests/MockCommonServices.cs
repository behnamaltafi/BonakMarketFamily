using System;
using System.Reflection;
using Autofac;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BonakMarket.Caching;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Logging;
using BonakMarket.Core.OutputCache;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.Core.Web;
using BonakMarket.Diagnostics;
using BonakMarket.Engine;
using BonakMarket.Events;

namespace BonakMarket.Core.Tests;

internal class MockCommonServices : ICommonServices
{
    static MockCommonServices()
    {
        // Autofac contains an unreplicable attribute 'NullableContextAttribute'.
        // Add it to Castle Proxy "avoid" list. 
        // https://stackoverflow.com/questions/64011323/cant-stub-class-with-nullablecontextattribute
        var assemblies = new Assembly[] { typeof(IComponentContext).Assembly };

        foreach (var assembly in assemblies)
        {
            var attrTypeToAvoid = Array.Find(assembly.GetTypes(), t => t.Name == "NullableContextAttribute");
            if (attrTypeToAvoid != null)
            {
                Castle.DynamicProxy.Generators.AttributesToAvoidReplicating.Add(attrTypeToAvoid);
                Console.WriteLine($"Found attribute to avoid: {attrTypeToAvoid.AssemblyQualifiedNameWithoutVersion()}");
            }
        }
    }

    private SmartDbContext _db;
    private IComponentContext _container;

    public MockCommonServices(SmartDbContext db)
        : this(db, new Mock<IComponentContext>().Object)
    {
    }

    public MockCommonServices(SmartDbContext db, IComponentContext container)
    {
        _db = Guard.NotNull(db, nameof(db));
        _container = Guard.NotNull(container, nameof(container));
    }

    public IComponentContext Container => _container;
    public IApplicationContext ApplicationContext => EngineContext.Current.Application;
    public ICacheFactory CacheFactory => new Mock<ICacheFactory>().Object;
    public ICacheManager Cache => NullCache.Instance;
    public IRequestCache RequestCache => NullRequestCache.Instance;
    public SmartDbContext DbContext => _db;
    public IStoreContext StoreContext => new Mock<IStoreContext>().Object;
    public IWorkContext WorkContext => new Mock<IWorkContext>().Object;
    public IWebHelper WebHelper => new Mock<IWebHelper>().Object;
    public IEventPublisher EventPublisher => NullEventPublisher.Instance;
    public ILocalizationService Localization => new Mock<ILocalizationService>().Object;
    public ISettingService Settings => new Mock<ISettingService>().Object;
    public ISettingFactory SettingFactory => new Mock<ISettingFactory>().Object;
    public ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;
    public IActivityLogger ActivityLogger => new Mock<IActivityLogger>().Object;
    public INotifier Notifier => new Notifier();
    public IPermissionService Permissions => new Mock<IPermissionService>().Object;
    public IChronometer Chronometer => NullChronometer.Instance;
    public IDateTimeHelper DateTimeHelper => new Mock<IDateTimeHelper>().Object;
    public IMediaService MediaService => new Mock<IMediaService>().Object;
    public IDisplayControl DisplayControl => new Mock<IDisplayControl>().Object;
    public ICurrencyService CurrencyService => new Mock<ICurrencyService>().Object;
    public IStoreMappingService StoreMappingService => new Mock<IStoreMappingService>().Object;
}