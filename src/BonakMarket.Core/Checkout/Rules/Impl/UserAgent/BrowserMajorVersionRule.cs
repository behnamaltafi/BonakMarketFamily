using BonakMarket.Core.Rules;
using BonakMarket.Core.Web;

namespace BonakMarket.Core.Checkout.Rules.Impl;

internal class BrowserMajorVersionRule : IRule<CartRuleContext>
{
    private readonly IUserAgent _userAgent;

    public BrowserMajorVersionRule(IUserAgent userAgent)
    {
        _userAgent = userAgent;
    }

    public Task<bool> MatchAsync(CartRuleContext context, RuleExpression expression)
    {
        var match = false;

        if (_userAgent.Version is Version version && version.Major > 0)
        {
            match = expression.Operator.Match(version.Major, expression.Value);
        }

        return Task.FromResult(match);
    }
}