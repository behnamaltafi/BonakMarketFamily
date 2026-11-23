using Autofac;
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
using BonakMarket.Events;

namespace BonakMarket.Core;

public interface ICommonServices
{
    IComponentContext Container { get; }
    IApplicationContext ApplicationContext { get; }
    ICacheFactory CacheFactory { get; }
    ICacheManager Cache { get; }
    IRequestCache RequestCache { get; }
    SmartDbContext DbContext { get; }
    IStoreContext StoreContext { get; }
    IWorkContext WorkContext { get; }
    IWebHelper WebHelper { get; }
    IEventPublisher EventPublisher { get; }
    ILocalizationService Localization { get; }
    ISettingService Settings { get; }
    ISettingFactory SettingFactory { get; }
    ILoggerFactory LoggerFactory { get; }
    IActivityLogger ActivityLogger { get; }
    INotifier Notifier { get; }
    IPermissionService Permissions { get; }
    IChronometer Chronometer { get; }
    IDateTimeHelper DateTimeHelper { get; }
    IMediaService MediaService { get; }
    IDisplayControl DisplayControl { get; }
    ICurrencyService CurrencyService { get; }
    IStoreMappingService StoreMappingService { get; }
}

public static class ICommonServicesExtensions
{
    public static TService Resolve<TService>(this ICommonServices services)
        => services.Container.Resolve<TService>();

    public static TService ResolveKeyed<TService>(this ICommonServices services, object serviceKey)
        => services.Container.ResolveKeyed<TService>(serviceKey);

    public static TService ResolveNamed<TService>(this ICommonServices services, string serviceName)
        => services.Container.ResolveNamed<TService>(serviceName);
}