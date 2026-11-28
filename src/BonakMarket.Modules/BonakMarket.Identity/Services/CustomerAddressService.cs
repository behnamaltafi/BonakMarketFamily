using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Common;
using BonakMarket.Core.Common.Configuration;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Stores;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Queries;

namespace BonakMarket.Identity.Services;

public sealed class CustomerAddressService(SmartDbContext db, AddressSettings settings, IStoreContext store, IAddressService addressService)
{
    private IQueryable<Address> Owned(Customer customer) =>
        db.Customers.Where(c => c.Id == customer.Id).SelectMany(c => c.Addresses);

    private async Task<Country> IranAsync(CancellationToken ct) =>
        await db.Countries.Where(c => c.TwoLetterIsoCode == "IR" && c.Published)
            .ApplyStandardFilter(false, store.CurrentStore.Id).SingleOrDefaultAsync(ct);

    private Dictionary<string, AddressField> Fields() => new()
    {
        ["firstName"] = new(true, true), ["lastName"] = new(true, true),
        ["phoneNumber"] = new(settings.PhoneEnabled, settings.PhoneEnabled && settings.PhoneRequired),
        ["salutation"] = new(settings.SalutationEnabled, false),
        ["title"] = new(settings.TitleEnabled, false),
        ["company"] = new(settings.CompanyEnabled, settings.CompanyEnabled && settings.CompanyRequired),
        ["faxNumber"] = new(settings.FaxEnabled, settings.FaxEnabled && settings.FaxRequired),
        ["stateProvinceId"] = new(true, true),
        ["city"] = new(true, false),
        ["cityId"] = new(true, true),
        ["latitude"] = new(true, false),
        ["longitude"] = new(true, false),
        ["address1"] = new(settings.StreetAddressEnabled, settings.StreetAddressEnabled && settings.StreetAddressRequired),
        ["address2"] = new(settings.StreetAddress2Enabled, settings.StreetAddress2Enabled && settings.StreetAddress2Required),
        ["zipPostalCode"] = new(settings.ZipPostalCodeEnabled, settings.ZipPostalCodeEnabled && settings.ZipPostalCodeRequired)
    };

    public async Task<LocationPage<AddressLocation>> ProvincesAsync(LocationListRequest request, CancellationToken ct)
    {
        var country = await IranAsync(ct);
        if (country == null) return new([], request.Page, request.PageSize, 0, 0);
        return await PageAsync(ProvinceQuery(country.Id, request), request, ct);
    }

    public async Task<LocationPage<CityLocation>> CitiesAsync(int provinceId, LocationListRequest request, CancellationToken ct)
    {
        var country = await IranAsync(ct);
        if (country == null) return new([], request.Page, request.PageSize, 0, 0);
        var query = LocationQueries.Cities(db.Cities.AsNoTracking(), country.Id, provinceId, request)
            .Select(c => new CityLocation(c.Id, c.Name, c.StateProvinceId));
        return await PageAsync(query, request, ct);
    }

    private IQueryable<AddressLocation> ProvinceQuery(int countryId, LocationListRequest request) =>
        LocationQueries.Provinces(db.StateProvinces.AsNoTracking(), db.Cities.AsNoTracking(), countryId, request)
            .Select(p => new AddressLocation(p.Id, p.Name));

    private static async Task<LocationPage<T>> PageAsync<T>(IQueryable<T> query, LocationListRequest request, CancellationToken ct)
    {
        var page = await query.ToPagedList(request.Page - 1, request.PageSize).LoadAsync(cancelToken: ct);
        return new(page.ToList(), page.PageNumber, page.PageSize, page.TotalCount, page.TotalPages);
    }

    public async Task<CustomerAddressSettings> GetSettingsAsync(CancellationToken ct)
    {
        var country = await IranAsync(ct);
        if (country == null) return null;
        var provinces = await ProvinceQuery(country.Id, new LocationListRequest()).ToListAsync(ct);
        return new(new(country.Id, country.Name), Fields(), provinces,
            (settings.Salutations ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    public async Task<AddressPage> ListAsync(Customer customer, int page, int pageSize, CancellationToken ct)
    {
        var query = Owned(customer).AsNoTracking();
        var count = await query.CountAsync(ct);
        var rows = await Project(customer, query.OrderBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(ct);
        return new(rows, page, pageSize, count);
    }

    public async Task<List<CustomerAddressDto>> LegacyListAsync(Customer customer, CancellationToken ct)
    {
        return await Project(customer, Owned(customer).AsNoTracking().OrderBy(a => a.Id)).ToListAsync(ct);
    }

    public async Task<CustomerAddressDto> GetAsync(Customer customer, int id, CancellationToken ct)
    {
        return await Project(customer, Owned(customer).AsNoTracking().Where(a => a.Id == id))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<bool> SharedAsync(Customer customer, int id, CancellationToken ct) =>
        await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Id != customer.Id &&
            (c.Addresses.Any(a => a.Id == id) || c.BillingAddressId == id || c.ShippingAddressId == id), ct) ||
        await db.Orders.IgnoreQueryFilters().AnyAsync(o => o.BillingAddressId == id || o.ShippingAddressId == id, ct);

    public async Task<(CustomerAddressDto Address, Dictionary<string, string[]> Errors, int Status)> SaveAsync(
        Customer customer, int? id, AddressUpdateRequest input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var country = await IranAsync(ct);
        if (country == null) return (null, Error("country", "iran_not_configured"), 409);
        var row = id.HasValue ? await Owned(customer).FirstOrDefaultAsync(a => a.Id == id.Value, ct) : new Address();
        if (row == null) return (null, null, 404);
        if (id.HasValue && await SharedAsync(customer, row.Id, ct))
            return (null, Error("address", "address_shared"), 409);
        var fields = Fields();
        var errors = new Dictionary<string, string[]>();
        string Read(string key, string value)
        {
            if (!fields[key].Enabled) return null;
            value = value?.Trim();
            if (fields[key].Required && string.IsNullOrEmpty(value)) errors[key] = ["required"];
            return string.IsNullOrEmpty(value) ? null : value;
        }
        var first = Read("firstName", input.FirstName);
        var last = Read("lastName", input.LastName);
        var phone = Read("phoneNumber", input.PhoneNumber);
        var company = Read("company", input.Company);
        var title = Read("title", input.Title);
        var salutation = Read("salutation", input.Salutation);
        var fax = Read("faxNumber", input.FaxNumber);
        var city = input.City?.Trim();
        var address1 = Read("address1", input.Address1);
        var address2 = Read("address2", input.Address2);
        var zip = Read("zipPostalCode", input.ZipPostalCode);
        var province = input.StateProvinceId > 0 ? input.StateProvinceId : null;
        if (province == null) errors["stateProvinceId"] = ["required"];
        if (province.HasValue && !await db.StateProvinces.AnyAsync(p => p.Id == province && p.CountryId == country.Id && p.Published, ct))
            errors["stateProvinceId"] = ["invalid_province"];

        // Legacy clients can submit the existing city name; resolve it to the same city record.
        var cityId = input.CityId ?? (id.HasValue && row.StateProvinceId == province &&
            (string.IsNullOrEmpty(city) || city == row.City) ? row.CityId : null);
        var available = db.Cities.Where(c => c.StateProvinceId == province && c.Published);
        var selectedCity = cityId.HasValue
            ? await available.SingleOrDefaultAsync(c => c.Id == cityId.Value, ct)
            : await available.FirstOrDefaultAsync(c => c.Name == city ||
                (city == "Tehran" && c.Name == "تهران") || (city == "تهران" && c.Name == "Tehran"), ct);
        // Preserve historical unclassified addresses when their city/province were not changed.
        var unchangedLegacy = id.HasValue && !input.CityId.HasValue && row.CityId == null &&
            row.StateProvinceId == province && !string.IsNullOrEmpty(city) && city == row.City;
        if (selectedCity == null && !unchangedLegacy) errors["cityId"] = ["invalid_city"];

        var latitude = row.Latitude;
        var longitude = row.Longitude;
        if (input.LatitudeSpecified || input.LongitudeSpecified)
        {
            if (!input.LatitudeSpecified || !input.LongitudeSpecified ||
                input.Latitude.HasValue != input.Longitude.HasValue ||
                input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180)
                errors["location"] = ["invalid_coordinates"];
            latitude = input.Latitude;
            longitude = input.Longitude;
        }
        if (errors.Count != 0) return (null, errors, 422);
        var makeDefault = input.IsDefault || !await Owned(customer).AnyAsync(ct);
        if (makeDefault && (!country.AllowsBilling || !country.AllowsShipping))
            return (null, Error("isDefault", "country_not_allowed"), 422);
        row.FirstName = first; row.LastName = last; row.PhoneNumber = phone;
        row.CountryId = country.Id; row.StateProvinceId = province;
        row.CityId = selectedCity?.Id; row.City = selectedCity?.Name ?? city;
        row.Latitude = latitude; row.Longitude = longitude;
        row.Address1 = address1; row.Address2 = address2; row.ZipPostalCode = zip;
        row.Salutation = salutation; row.Title = title; row.Company = company; row.FaxNumber = fax;
        row.Email = customer.Email; // Internal placeholder stays internal; never returned by the address API.
        if (!id.HasValue) customer.Addresses.Add(row);
        await db.SaveChangesAsync(ct);
        if (makeDefault)
        {
            SetDefault(customer, row.Id);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return (await GetAsync(customer, row.Id, ct), null, id.HasValue ? 200 : 201);
    }

    public async Task<int> DefaultAsync(Customer customer, int id, CancellationToken ct)
    {
        var row = await Owned(customer).Include(a => a.Country).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row == null) return 404;
        if (row.Country?.TwoLetterIsoCode != "IR" || !row.Country.Published ||
            !row.Country.AllowsBilling || !row.Country.AllowsShipping || !await addressService.IsAddressValidAsync(row)) return 422;
        SetDefault(customer, id);
        await db.SaveChangesAsync(ct);
        return 200;
    }

    public async Task<int> DeleteAsync(Customer customer, int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var row = await Owned(customer).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row == null) return 404;
        var ga = customer.GenericAttributes;
        var wasDefault = ga.DefaultBillingAddressId == id || ga.DefaultShippingAddressId == id;
        customer.RemoveAddress(row);
        // Detach only. Native addresses can be shared by ERP/OData or referenced by historical orders.
        // Never delete or mutate an order snapshot.
        if (wasDefault)
        {
            var candidates = await Owned(customer).Where(a => a.Id != id && a.Country != null &&
                a.Country.TwoLetterIsoCode == "IR" && a.Country.Published && a.Country.AllowsBilling && a.Country.AllowsShipping)
                .Include(a => a.Country).Include(a => a.StateProvince).OrderBy(a => a.Id).ToListAsync(ct);
            int? replacement = null;
            foreach (var candidate in candidates)
            {
                if (await addressService.IsAddressValidAsync(candidate)) { replacement = candidate.Id; break; }
            }
            SetDefault(customer, replacement);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return 204;
    }

    private static void SetDefault(Customer customer, int? id)
    {
        customer.GenericAttributes.DefaultBillingAddressId = id;
        customer.GenericAttributes.DefaultShippingAddressId = id;
    }

    private static Dictionary<string, string[]> Error(string field, string code) => new() { [field] = [code] };
    private IQueryable<CustomerAddressDto> Project(Customer customer, IQueryable<Address> addresses) =>
        CustomerAddressQueries.Project(addresses, db.Cities.AsNoTracking(),
            customer.GenericAttributes.DefaultBillingAddressId, customer.GenericAttributes.DefaultShippingAddressId);
}
