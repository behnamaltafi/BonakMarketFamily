using BonakMarket.Core.Seo;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<UrlRecord> UrlRecords { get; set; }
}