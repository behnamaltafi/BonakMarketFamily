using BonakMarket.Core.Common;
using BonakMarket.Identity.Models;

namespace BonakMarket.Identity.Queries;

/// <summary>Projects current customer addresses without modifying stored order snapshots.</summary>
public static class CustomerAddressQueries
{
    /// <summary>Uses the authoritative city name, retaining legacy text when no city is linked.</summary>
    public static IQueryable<CustomerAddressDto> Project(IQueryable<Address> addresses, IQueryable<City> cities,
        int? defaultBillingAddressId, int? defaultShippingAddressId)
    {
        return addresses.Select(a => new CustomerAddressDto(a.Id, a.FirstName, a.LastName, a.CountryId,
            a.StateProvinceId, cities.Where(c => c.Id == a.CityId).Select(c => c.Name).FirstOrDefault() ?? a.City,
            a.Address1, a.Address2, a.ZipPostalCode, a.PhoneNumber,
            a.Country == null ? null : a.Country.Name, a.StateProvince == null ? null : a.StateProvince.Name,
            defaultBillingAddressId == a.Id, defaultShippingAddressId == a.Id,
            a.Salutation, a.Title, a.Company, a.FaxNumber, a.CityId, a.Latitude, a.Longitude));
    }
}
