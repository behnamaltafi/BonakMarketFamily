using BonakMarket.Core.Content.Topics;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<Topic> Topics { get; set; }
}