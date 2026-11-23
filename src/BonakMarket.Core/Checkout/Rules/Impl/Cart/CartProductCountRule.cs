using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class CartProductCountRule : IRule<CartRuleContext>
{
    public Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var productCount = context.ShoppingCart.GetTotalQuantity();

        return Task.FromResult(expression.Operator.Match(productCount, expression.Value));
    }
}