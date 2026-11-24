namespace BonakMarket.Core.Rules;

public interface IRuleConstraint
{
    bool Match(RuleExpression expression);
}