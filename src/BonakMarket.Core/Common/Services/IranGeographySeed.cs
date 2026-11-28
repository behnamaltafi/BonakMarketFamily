using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;

namespace BonakMarket.Core.Common.Services;

internal static class IranGeographySeed
{
    internal static async Task SeedAsync(SmartDbContext db, CancellationToken ct)
    {
        // Fresh installation seeds countries before calling this helper.
        // Do not insert a duplicate country during an early migration on an empty database.
        var iran = await db.Countries.SingleOrDefaultAsync(x => x.TwoLetterIsoCode == "IR", ct);
        if (iran == null)
        {
            if (!await db.Countries.AnyAsync(ct)) return;
            iran = new Country
            {
                Name = "ایران", TwoLetterIsoCode = "IR", ThreeLetterIsoCode = "IRN", NumericIsoCode = 364,
                Published = true, AllowsBilling = true, AllowsShipping = true, DiallingCode = 98
            };
            db.Countries.Add(iran);
            await db.SaveChangesAsync(ct);
        }

        var province = await db.StateProvinces.Where(x => x.CountryId == iran.Id &&
            (x.Name == "تهران" || x.Name == "Tehran" || x.Name == "Tehran Province" || x.Abbreviation == "IR-23"))
            .OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (province == null)
        {
            province = new StateProvince { CountryId = iran.Id, Name = "تهران", Published = true };
            db.StateProvinces.Add(province);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Cities.AnyAsync(x => x.StateProvinceId == province.Id &&
            (x.Name == "تهران" || x.Name == "Tehran"), ct))
        {
            db.Cities.Add(new City { StateProvinceId = province.Id, Name = "تهران", Published = true });
            await db.SaveChangesAsync(ct);
        }
    }
}
