using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class CustomerVatNumberStatusRule : IRule<CartRuleContext>
{
    public Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var match = expression.HasListMatch(context.Customer?.VatNumberStatusId ?? -1);
        return Task.FromResult(match);
    }
}