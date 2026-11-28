using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using BonakMarket.Core.Common;
using BonakMarket.Core.Common.Services;

namespace BonakMarket.Core.Tests.Common;

[TestFixture]
public class IranGeographySeedTests : ServiceTestBase
{
    [Test]
    public async Task Repeated_seed_reuses_iran_and_tehran()
    {
        var iran = new Country
        {
            Name = "Iran", TwoLetterIsoCode = "IR", ThreeLetterIsoCode = "IRN",
            NumericIsoCode = 364, Published = true, AllowsBilling = true, AllowsShipping = true
        };
        DbContext.Countries.Add(iran);
        await DbContext.SaveChangesAsync();
        var province = new StateProvince { CountryId = iran.Id, Name = "Tehran", Published = true };
        DbContext.StateProvinces.Add(province);
        await DbContext.SaveChangesAsync();

        await IranGeographySeed.SeedAsync(DbContext, CancellationToken.None);
        await IranGeographySeed.SeedAsync(DbContext, CancellationToken.None);

        Assert.That(await DbContext.Countries.CountAsync(c => c.TwoLetterIsoCode == "IR"), Is.EqualTo(1));
        Assert.That(await DbContext.StateProvinces.CountAsync(s => s.CountryId == iran.Id), Is.EqualTo(1));
        var city = await DbContext.Cities.SingleAsync(c => c.StateProvinceId == province.Id);
        Assert.That(city.Published, Is.True);
        Assert.That(city.Name, Is.EqualTo("تهران"));
    }
}
