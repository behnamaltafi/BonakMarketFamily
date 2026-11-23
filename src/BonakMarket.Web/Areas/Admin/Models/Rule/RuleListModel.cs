using BonakMarket.Core.Rules;

namespace BonakMarket.Admin.Models.Rules;

public class RuleListModel : ModelBase
{
    public RuleScope? Scope { get; set; }
    public string ScopeName { get; set; }
}