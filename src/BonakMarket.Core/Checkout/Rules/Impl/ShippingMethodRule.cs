using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class ShippingMethodRule : IRule<CartRuleContext>
{
    public Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var shippingMethod = context.Customer.GenericAttributes.Get<ShippingOption>(SystemCustomerAttributeNames.SelectedShippingOption, context.Store.Id);
        var match = expression.HasListMatch(shippingMethod?.ShippingMethodId ?? 0);

        return Task.FromResult(match);
    }
}