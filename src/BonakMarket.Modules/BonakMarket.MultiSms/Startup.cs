using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Data;
using BonakMarket.Core.Messaging;
using BonakMarket.Data;
using BonakMarket.Data.Providers;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.MultiSms.Domain;
using BonakMarket.MultiSms.Services;

namespace BonakMarket.MultiSms;

/// <summary>
/// Registers SMS transport providers and SQL delivery history only.
/// </summary>
internal sealed class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddSingleton<SmsCredentialProtection>();
        services.AddSingleton<ISmsProvider, KavenegarSmsProvider>();
        services.AddSingleton<ISmsProvider, MelipayamakSmsProvider>();
        services.AddSingleton<ISmsProvider, SmsIrSmsProvider>();
        services.AddSingleton<DevelopmentSmsInbox>();
        services.AddSingleton<ISmsProvider, MockSmsProvider>();
        services.AddSingleton<OtpHistoryProtection>();
        services.AddScoped<SmsHistoryStore>();
        services.AddScoped<ISmsHistoryWriter>(sp => sp.GetRequiredService<SmsHistoryStore>());
        services.AddScoped<ISmsSender, SmsGatewaySender>();
        services.AddScoped<ISmsDeliveryAvailability, SmsDeliveryAvailability>();
        services.AddTransient<IDbContextConfigurationSource<SmartDbContext>, MultiSmsModelConfigurer>();
    }

    private sealed class MultiSmsModelConfigurer : IDbContextConfigurationSource<SmartDbContext>
    {
        public void Configure(IServiceProvider services, DbContextOptionsBuilder builder)
        {
            builder.UseDbFactory(factory => factory.AddModelAssembly(typeof(SmsMessage).Assembly));
        }
    }
}
