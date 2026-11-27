#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record OrderAddressDto(int Id, string? FirstName, string? LastName, string? Company,
    string? Country, string? StateProvince, string? City, string? Address1, string? Address2,
    string? ZipPostalCode, string? PhoneNumber, string? Email);
