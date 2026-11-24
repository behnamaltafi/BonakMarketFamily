using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Google.MerchantCenter.Domain;

namespace BonakMarket.Google.MerchantCenter;

public static class SmartDbContextExtensions
{
    public static DbSet<GoogleProduct> GoogleProducts(this SmartDbContext db)
        => db.Set<GoogleProduct>();
}