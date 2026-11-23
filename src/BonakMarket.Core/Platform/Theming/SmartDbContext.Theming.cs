using BonakMarket.Core.Theming;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<ThemeVariable> ThemeVariables { get; set; }
}