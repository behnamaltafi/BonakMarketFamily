using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Rules;
using BonakMarket.Threading;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class CartWeightRule(IShippingService shippingService) : IRule<CartRuleContext>
{
    private readonly IShippingService _shippingService = shippingService;

    public async Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var lockKey = "rule:cart:cartweightrule:" + context.SessionKey.ToString();
        if (AsyncLock.IsLockHeld(lockKey))
        {
            return false;
        }

        // Prevent the rule from indirectly calling itself when checkout attribute weights are materialized.
        using (await AsyncLock.KeyedAsync(lockKey))
        {
            var weight = await _shippingService.GetCartTotalWeightAsync(context.ShoppingCart, cache: false);
            return expression.Operator.Match(weight, expression.Value);
        }
    }
}