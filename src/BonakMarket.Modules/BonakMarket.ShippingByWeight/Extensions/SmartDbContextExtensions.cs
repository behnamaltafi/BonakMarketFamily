using BonakMarket.Core.Data;

namespace BonakMarket.ShippingByWeight;

public static class SmartDbContextExtensions
{
    public static DbSet<ShippingRateByWeight> ShippingRatesByWeight(this SmartDbContext db)
        => db.Set<ShippingRateByWeight>();
}