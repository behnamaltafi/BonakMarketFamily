using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Engine.Initialization;
using BonakMarket.Events;
using BonakMarket.Scheduling;
using BonakMarket.StorefrontApi.Domain;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

internal sealed class GuestCartTaskInitializer(ITaskStore store, ILogger<GuestCartTaskInitializer> logger) : IApplicationInitializer
{
    public int Order => 10;
    public bool ThrowOnError => false;
    public int MaxAttempts => 3;
    public async Task InitializeAsync(HttpContext httpContext)
    {
        var task = await store.GetOrAddTaskAsync<GuestCartCleanupTask>(x =>
        {
            x.Name = "Storefront guest cart cleanup";
            x.CronExpression = "*/15 * * * *";
            x.Enabled = true;
            x.StopOnError = false;
            x.RunPerMachine = false;
        });
        if (task.Enabled && !task.NextRunUtc.HasValue)
            await store.CalculateFutureSchedulesAsync(new[] { task });
    }
    public Task OnFailAsync(Exception exception, bool willRetry)
    {
        logger.LogWarning("Guest cart cleanup registration failed: {ErrorType}", exception.GetType().Name);
        return Task.CompletedTask;
    }
}
