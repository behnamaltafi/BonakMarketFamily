using BonakMarket.Core.Configuration;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<Setting> Settings { get; set; }
}