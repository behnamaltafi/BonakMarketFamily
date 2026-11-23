using BonakMarket.Core.Data;

namespace BonakMarket.Shipping;

public static class SmartDbContextExtensions
{
    public static DbSet<ShippingRateByTotal> ShippingRatesByTotal(this SmartDbContext db)
        => db.Set<ShippingRateByTotal>();
}