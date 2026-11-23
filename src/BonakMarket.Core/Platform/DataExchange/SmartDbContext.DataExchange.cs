using BonakMarket.Core.DataExchange;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<SyncMapping> SyncMappings { get; set; }
    public DbSet<ImportProfile> ImportProfiles { get; set; }
    public DbSet<ExportProfile> ExportProfiles { get; set; }
    public DbSet<ExportDeployment> ExportDeployments { get; set; }
}