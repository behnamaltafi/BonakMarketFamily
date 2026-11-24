using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Queries;

public sealed class CustomerQueries(SmartDbContext db, StorefrontRequestContext request)
{
    public Task<List<AddressDto>> AddressesAsync(CancellationToken ct) =>
        db.Customers.AsNoTracking().Where(x => x.Id == request.Customer.Id)
            .SelectMany(x => x.Addresses).OrderBy(x => x.Id)
            .Select(x => new AddressDto(x.Id, x.FirstName, x.LastName, x.CountryId, x.StateProvinceId,
                x.City, x.Address1, x.Address2, x.ZipPostalCode, x.PhoneNumber)).ToListAsync(ct);
}
