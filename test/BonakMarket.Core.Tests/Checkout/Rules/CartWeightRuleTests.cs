using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Rules;
using BonakMarket.Core.Checkout.Rules.Impl;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Tests.Checkout.Rules;

[TestFixture]
public class CartWeightRuleTests
{
    [Test]
    public async Task Uses_uncached_cart_weight()
    {
        var cart = new ShoppingCart(new Customer { Id = 1 }, 1, []);
        var shippingService = new Mock<IShippingService>();
        shippingService
            .Setup(x => x.GetCartTotalWeightAsync(cart, true, false))
            .ReturnsAsync(10m);

        var rule = new CartWeightRule(shippingService.Object);
        var context = new CartRuleContext(() => 1) { ShoppingCart = cart };
        var expression = new RuleExpression
        {
            Operator = RuleOperator.GreaterThan,
            Value = 5m
        };

        var result = await rule.MatchAsync(context, expression);

        Assert.That(result, Is.True);
        shippingService.Verify(x => x.GetCartTotalWeightAsync(cart, true, false), Times.Once);
    }
}