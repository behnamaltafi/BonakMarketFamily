using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class ProductReviewCountRule : IRule<CartRuleContext>
{
    private readonly SmartDbContext _db;

    public ProductReviewCountRule(SmartDbContext db)
    {
        _db = db;
    }

    public async Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var reviewsCount = await _db.CustomerContent
            .ApplyCustomerFilter(context.Customer.Id, true)
            .OfType<ProductReview>()
            .CountAsync();

        return expression.Operator.Match(reviewsCount, expression.Value);
    }
}