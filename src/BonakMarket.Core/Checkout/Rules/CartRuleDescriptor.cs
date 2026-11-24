using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules;

public class CartRuleDescriptor : RuleDescriptor
{
    public CartRuleDescriptor()
        : base(RuleScope.Cart)
    {
    }

    public Type ProcessorType { get; init; }
}