using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class RewardPointsBalanceRule : IRule<CartRuleContext>
{
    public Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var rewardPointsBalance = context.Customer.GetRewardPointsBalance();
        return Task.FromResult(expression.Operator.Match(rewardPointsBalance, expression.Value));
    }
}