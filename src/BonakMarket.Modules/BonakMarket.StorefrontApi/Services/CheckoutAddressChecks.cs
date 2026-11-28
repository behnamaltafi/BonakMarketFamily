#nullable enable
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Common;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class CheckoutAddressChecks(SmartDbContext db, StorefrontRequestContext request, IAddressService addresses)
{
    internal Task<Address?> OwnedAddress(int id, CancellationToken ct) =>
        db.Customers.AsNoTracking().Where(x => x.Id == request.Customer.Id).SelectMany(x => x.Addresses)
            .Include(x => x.Country).Include(x => x.StateProvince).FirstOrDefaultAsync(x => x.Id == id, ct);

    internal async Task<bool> ValidAddress(Address address, bool forShipping) =>
        address.Country?.TwoLetterIsoCode == "IR" && address.Country.Published &&
        (forShipping ? address.Country.AllowsShipping : address.Country.AllowsBilling) &&
        await addresses.IsAddressValidAsync(address);

    internal static string? AddressFingerprint(Address? address) => address == null ? null :
        CartSnapshotRevision.Create(new { address.Id, address.CountryId, address.StateProvinceId, address.City, address.CityId, address.Latitude, address.Longitude,
            address.Address1, address.Address2, address.ZipPostalCode, address.PhoneNumber, address.FirstName,
            address.LastName, address.Email, address.Company, address.FaxNumber, address.Salutation, address.Title });

    internal async Task<bool> AddressesCurrent(CheckoutDraft draft, CancellationToken ct)
    {
        if (draft.BillingAddressId.HasValue)
        {
            var address = await OwnedAddress(draft.BillingAddressId.Value, ct);
            if (address == null || AddressFingerprint(address) != draft.BillingFingerprint || !await ValidAddress(address, false))
                return false;
        }
        if (draft.ShippingAddressId.HasValue)
        {
            var address = await OwnedAddress(draft.ShippingAddressId.Value, ct);
            if (address == null || AddressFingerprint(address) != draft.ShippingFingerprint || !await ValidAddress(address, true))
                return false;
        }
        return true;
    }
}
