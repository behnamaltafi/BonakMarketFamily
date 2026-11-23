#nullable enable

using BonakMarket.Admin.Components;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Security;
using BonakMarket.Core.Widgets;
using BonakMarket.Core.Widgets.Dashboard;
using BonakMarket.Engine.Modularity;

namespace BonakMarket.Admin.Infrastructure.Dashboard;

/// <summary>
/// Describes and creates the dashboard widget that renders the BonakMarket news feed.
/// </summary>
[SystemName(SystemName)]
public sealed class NewsFeedDashboardWidget(IPermissionService permissions)
    : DashboardViewComponentWidget<NewsFeedViewComponent>(permissions, NewsFeedViewComponent.RequiredPermission)
{
    /// <summary>
    /// Identifies the news feed dashboard widget type.
    /// </summary>
    public const string SystemName = "BonakMarket.Admin.Dashboard.NewsFeed";

    private static readonly DashboardWidgetDescriptor _descriptor = new(SystemName, ResolvableText.Resource("Admin.NewsFeed.Title"))
    {
        Group = KnownDashboardWidgetGroups.System,
        IconName = "newspaper",
        CssClass = "news-feed",
        Order = 800,
        DefaultSize = new DashboardWidgetSize(2, 1),
        MinSize = new DashboardWidgetSize(2, 1),
        MaxSize = new DashboardWidgetSize(12),
        Policy = new DashboardWidgetPolicy
        {
            IsRequired = true,
            AllowMove = false,
            AllowResize = false
        }
    };

    /// <inheritdoc />
    public override DashboardWidgetDescriptor Descriptor => _descriptor;

    /// <inheritdoc />
    public override TimeSpan? CacheDuration => TimeSpan.FromHours(4);
    /// <inheritdoc />
    public override Widget CreateWidget(DashboardWidgetContext context, DashboardWidgetInstance instance)
        => new ComponentWidget<NewsFeedViewComponent>(new { cacheDuration = CacheDuration, refresh = context.Refresh }) { Key = instance.Id };
}
