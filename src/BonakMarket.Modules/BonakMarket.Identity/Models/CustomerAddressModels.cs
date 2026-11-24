#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Identity.Models;

public sealed class AddressUpdateRequest
{
    [StringLength(225)] public string? FirstName { get; set; }
    [StringLength(225)] public string? LastName { get; set; }
    [StringLength(100)] public string? PhoneNumber { get; set; }
    [StringLength(50)] public string? Salutation { get; set; }
    [StringLength(100)] public string? Title { get; set; }
    [StringLength(255)] public string? Company { get; set; }
    [StringLength(100)] public string? FaxNumber { get; set; }
    public int? StateProvinceId { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(500)] public string? Address1 { get; set; }
    [StringLength(500)] public string? Address2 { get; set; }
    [StringLength(50)] public string? ZipPostalCode { get; set; }
    public bool IsDefault { get; set; }
}
public sealed record CustomerAddressDto(int Id, string? FirstName, string? LastName, int? CountryId,
    int? StateProvinceId, string? City, string? Address1, string? Address2, string? ZipPostalCode,
    string? PhoneNumber, string? CountryName, string? StateProvinceName, bool IsDefaultBilling,
    bool IsDefaultShipping, string? Salutation, string? Title, string? Company, string? FaxNumber);
public sealed record AddressPage(IReadOnlyList<CustomerAddressDto> Items, int Page, int PageSize, int TotalCount);
public sealed record AddressField(bool Enabled, bool Required);
public sealed record AddressLocation(int Id, string Name);
public sealed record CustomerAddressSettings(AddressLocation Country, IReadOnlyDictionary<string, AddressField> Fields,
    IReadOnlyList<AddressLocation> Provinces, IReadOnlyList<string> Salutations);
