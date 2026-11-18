using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Smartstore.Core.Data;
using Smartstore.Core.Messaging;
using Smartstore.Data;
using Smartstore.Engine;
using Smartstore.Engine.Builders;
using Smartstore.MultiSms.Domain;
using Smartstore.MultiSms.Services;

namespace Smartstore.MultiSms;

/// <summary>
/// Registers independent SMS transports and SQL-backed native Customer OTP.
/// </summary>
internal sealed class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddSingleton<SmsCredentialProtection>();
        services.AddSingleton<ISmsProvider, KavenegarSmsProvider>();
        services.AddSingleton<ISmsProvider, MelipayamakSmsProvider>();
        services.AddScoped<ISmsSender, SmsGatewaySender>();
        services.AddScoped<SmsOtpLoginService>();
        services.AddTransient<IDbContextConfigurationSource<SmartDbContext>, MultiSmsModelConfigurer>();
    }

    private sealed class MultiSmsModelConfigurer : IDbContextConfigurationSource<SmartDbContext>
    {
        public void Configure(IServiceProvider services, DbContextOptionsBuilder builder)
        {
            builder.UseDbFactory(factory => factory.AddModelAssembly(typeof(SmsLoginPhone).Assembly));
        }
    }
}
