using BonakMarket.Core.Data;

namespace BonakMarket.Tax;

public static class SmartDbContextExtensions
{
    public static DbSet<TaxRateEntity> TaxRates(this SmartDbContext db)
        => db.Set<TaxRateEntity>();
}