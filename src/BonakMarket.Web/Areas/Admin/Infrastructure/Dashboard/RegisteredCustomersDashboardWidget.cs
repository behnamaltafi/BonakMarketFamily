#nullable enable

using BonakMarket.Admin.Components;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Security;
using BonakMarket.Core.Widgets.Dashboard;
using BonakMarket.Engine.Modularity;

namespace BonakMarket.Admin.Infrastructure.Dashboard;

/// <summary>
/// Describes and creates the dashboard widget that renders customer registrations.
/// </summary>
[SystemName(SystemName)]
public sealed class RegisteredCustomersDashboardWidget(IPermissionService permissions)
    : DashboardViewComponentWidget<DashboardRegisteredCustomersViewComponent>(permissions, DashboardRegisteredCustomersViewComponent.RequiredPermission)
{
    /// <summary>
    /// Identifies the registered customers dashboard widget type.
    /// </summary>
    public const string SystemName = "BonakMarket.Admin.Dashboard.RegisteredCustomers";

    private static readonly DashboardWidgetDescriptor _descriptor = new(SystemName, ResolvableText.Resource("Admin.Report.CustomerRegistrations"))
    {
        Group = KnownDashboardWidgetGroups.Customers,
        IconName = "user-add",
        CssClass = "report-customer-registrations",
        Order = 500,
        DefaultSize = new DashboardWidgetSize(6, 1),
        MinSize = new DashboardWidgetSize(4, 1),
        MaxSize = new DashboardWidgetSize(12)
    };

    /// <inheritdoc />
    public override DashboardWidgetDescriptor Descriptor => _descriptor;

    /// <inheritdoc />
    public override TimeSpan? CacheDuration => TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override object GetWidgetArguments(DashboardWidgetContext context, DashboardWidgetInstance instance)
        => new { cacheDuration = CacheDuration, refresh = context.Refresh };
}
