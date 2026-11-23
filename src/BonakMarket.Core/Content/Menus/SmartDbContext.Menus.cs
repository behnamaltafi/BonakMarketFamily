using BonakMarket.Core.Content.Menus;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<MenuEntity> Menus { get; set; }
    public DbSet<MenuItemEntity> MenuItems { get; set; }
}