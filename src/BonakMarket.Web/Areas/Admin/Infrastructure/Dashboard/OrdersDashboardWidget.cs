#nullable enable

using BonakMarket.Admin.Components;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Security;
using BonakMarket.Core.Widgets.Dashboard;
using BonakMarket.Engine.Modularity;

namespace BonakMarket.Admin.Infrastructure.Dashboard;

/// <summary>
/// Describes and creates the dashboard widget that renders the orders report.
/// </summary>
[SystemName(SystemName)]
public sealed class OrdersDashboardWidget(IPermissionService permissions)
    : DashboardViewComponentWidget<DashboardOrdersViewComponent>(permissions, DashboardOrdersViewComponent.RequiredPermission)
{
    /// <summary>
    /// Identifies the orders dashboard widget type.
    /// </summary>
    public const string SystemName = "BonakMarket.Admin.Dashboard.Orders";

    private static readonly DashboardWidgetDescriptor _descriptor = new(SystemName, ResolvableText.Resource("Admin.Orders"))
    {
        Group = KnownDashboardWidgetGroups.Sales,
        IconName = "chart-line-arrow",
        CssClass = "report-orders",
        Order = 200,
        DefaultSize = new DashboardWidgetSize(6, 1),
        MinSize = new DashboardWidgetSize(4, 1),
        MaxSize = new DashboardWidgetSize(12)
    };

    /// <inheritdoc />
    public override DashboardWidgetDescriptor Descriptor => _descriptor;
}
