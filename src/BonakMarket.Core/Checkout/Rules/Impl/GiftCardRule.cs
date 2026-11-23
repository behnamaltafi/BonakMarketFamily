using BonakMarket.Core.Checkout.GiftCards;
using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class GiftCardRule(IGiftCardService giftCardService) : IRule<CartRuleContext>
{
    public async Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var giftCards = await giftCardService.GetValidGiftCardsAsync(context.Store.Id, context.Customer);
        var hasValidGiftCard = giftCards.Count > 0;

        return expression.Operator.Match(hasValidGiftCard, expression.Value);
    }
}
