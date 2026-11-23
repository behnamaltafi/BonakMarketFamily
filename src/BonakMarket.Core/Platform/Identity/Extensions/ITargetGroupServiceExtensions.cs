using System.Runtime.CompilerServices;
using BonakMarket.Collections;
using BonakMarket.Core.Rules;
using BonakMarket.Core.Rules.Filters;

namespace BonakMarket.Core.Identity.Rules;

public static partial class ITargetGroupServiceExtensions
{
    /// <summary>
    /// Processes a target group filter.
    /// </summary>
    /// <param name="targetGroupService">Target group service.</param>
    /// <param name="filter">Filter expressions.</param>
    /// <param name="pageIndex">Page index.</param>
    /// <param name="pageSize">Page size.</param>
    /// <returns>List of customers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPagedList<Customer> ProcessFilter(
        this ITargetGroupService targetGroupService,
        FilterExpression filter,
        int pageIndex = 0,
        int pageSize = int.MaxValue)
    {
        Guard.NotNull(targetGroupService, nameof(targetGroupService));
        Guard.NotNull(filter, nameof(filter));

        return targetGroupService.ProcessFilter(new[] { filter }, LogicalRuleOperator.And, pageIndex, pageSize);
    }
}