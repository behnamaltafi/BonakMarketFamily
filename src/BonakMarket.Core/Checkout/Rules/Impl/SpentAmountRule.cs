using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class SpentAmountRule : IRule<CartRuleContext>
{
    private readonly SmartDbContext _db;
    private readonly ICurrencyService _currencyService;
    private readonly IRoundingHelper _roundingHelper;

    public SpentAmountRule(
        SmartDbContext db,
        ICurrencyService currencyService,
        IRoundingHelper roundingHelper)
    {
        _db = db;
        _currencyService = currencyService;
        _roundingHelper = roundingHelper;
    }

    public async Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var query = _db.Orders
            .AsNoTracking()
            .ApplyStandardFilter(context.Customer.Id, context.Store.Id)
            .ApplyStatusFilter(new[] { (int)OrderStatus.Complete });

        var spentAmount = await query.SumAsync(x => (decimal?)x.OrderTotal);
        var roundedAmount = _roundingHelper.Round(spentAmount ?? 0m, _currencyService.PrimaryCurrency);

        return expression.Operator.Match(roundedAmount, expression.Value);
    }
}