using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class CustomerRoleRule : IRule<CartRuleContext>
{
    public Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var roleIds = context.Customer.GetRoleIds();
        var match = expression.HasListsMatch(roleIds);

        return Task.FromResult(match);
    }
}