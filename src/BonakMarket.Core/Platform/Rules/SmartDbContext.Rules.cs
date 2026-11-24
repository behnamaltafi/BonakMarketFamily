using BonakMarket.Core.Rules;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<RuleSetEntity> RuleSets { get; set; }
    public DbSet<RuleEntity> Rules { get; set; }
}