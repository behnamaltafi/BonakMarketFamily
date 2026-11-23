using Microsoft.AspNetCore.Http;
using BonakMarket.Scheduling;

namespace BonakMarket;

public static class SchedulerHttpExtensions
{
    /// <summary>
    /// Checks whether the current requests is called by the task scheduler.
    /// </summary>
    public static bool IsCalledByTaskScheduler(this HttpRequest request)
    {
        Guard.NotNull(request);
        return
            request.Headers.ContainsKey(DefaultTaskScheduler.AuthTokenName) ||
            request.Path.StartsWithSegments("/taskscheduler");
    }
}