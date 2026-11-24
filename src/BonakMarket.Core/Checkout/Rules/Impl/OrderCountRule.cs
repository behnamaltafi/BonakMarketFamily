using BonakMarket.Core.Data;
using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class OrderCountRule : IRule<CartRuleContext>
{
    private readonly SmartDbContext _db;

    public OrderCountRule(SmartDbContext db)
    {
        _db = db;
    }

    public async Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var count = await _db.Orders
            .ApplyStandardFilter(context.Customer.Id, context.Store.Id)
            .CountAsync();

        return expression.Operator.Match(count, expression.Value);
    }
}