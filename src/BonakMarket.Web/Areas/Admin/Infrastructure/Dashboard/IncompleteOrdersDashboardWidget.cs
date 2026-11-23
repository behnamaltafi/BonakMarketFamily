#nullable enable

using BonakMarket.Admin.Components;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Security;
using BonakMarket.Core.Widgets.Dashboard;
using BonakMarket.Engine.Modularity;

namespace BonakMarket.Admin.Infrastructure.Dashboard;

/// <summary>
/// Describes and creates the dashboard widget that renders incomplete orders.
/// </summary>
[SystemName(SystemName)]
public sealed class IncompleteOrdersDashboardWidget(IPermissionService permissions)
    : DashboardViewComponentWidget<DashboardIncompleteOrdersViewComponent>(permissions, DashboardIncompleteOrdersViewComponent.RequiredPermission)
{
    /// <summary>
    /// Identifies the incomplete orders dashboard widget type.
    /// </summary>
    public const string SystemName = "BonakMarket.Admin.Dashboard.IncompleteOrders";

    private static readonly DashboardWidgetDescriptor _descriptor = new(SystemName, ResolvableText.Resource("Admin.SalesReport.Incomplete"))
    {
        Group = KnownDashboardWidgetGroups.Sales,
        IconName = "hourglass",
        CssClass = "report-incomplete-orders",
        Order = 100,
        DefaultSize = new DashboardWidgetSize(10, 1),
        MinSize = new DashboardWidgetSize(4, 1),
        MaxSize = new DashboardWidgetSize(12)
    };

    /// <inheritdoc />
    public override DashboardWidgetDescriptor Descriptor => _descriptor;
}
