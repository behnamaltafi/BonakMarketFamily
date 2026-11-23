using BonakMarket.Core.Localization;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<Language> Languages { get; set; }
    public DbSet<LocaleStringResource> LocaleStringResources { get; set; }
    public DbSet<LocalizedProperty> LocalizedProperties { get; set; }
}