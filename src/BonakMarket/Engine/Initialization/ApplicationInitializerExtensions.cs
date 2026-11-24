using Microsoft.AspNetCore.Builder;
using BonakMarket.Engine.Initialization;

namespace BonakMarket.Bootstrapping;

public static class ApplicationInitializerExtensions
{
    /// <summary>
    /// Executes <see cref="IApplicationInitializer"/> implementations during the very first request.
    /// </summary>
    public static IApplicationBuilder UseApplicationInitializer(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApplicationInitializerMiddleware>();
    }
}