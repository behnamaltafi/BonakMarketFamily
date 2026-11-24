#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Identity.Models;

public sealed class ProfileUpdateRequest
{
    [StringLength(225)] public string? FirstName { get; set; }
    [StringLength(225)] public string? LastName { get; set; }
    [EmailAddress, StringLength(500)] public string? Email { get; set; }
    [StringLength(10)] public string? NationalCode { get; set; }
    public int? GuildId { get; set; }
}

public sealed record GuildDto(int Id, string Code, string Title, bool IsActive);
public sealed record GuildOptionDto(int Id, string Title);
public sealed record CustomerProfileDto(int Id, string? Username, string? FirstName, string? LastName, string? Email, string? Phone, bool ProfileRequired,
    string? NationalCode, GuildDto? Guild, string[] MissingRequiredFields);
public sealed record CustomerAddressDto(int Id, string? FirstName, string? LastName, int? CountryId, int? StateProvinceId, string? City, string? Address1, string? Address2, string? ZipPostalCode, string? PhoneNumber);
