#nullable enable

using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Identity.Models;

/// <summary>Authenticated current-customer profile update.</summary>
public sealed class ProfileUpdateRequest
{
    [Required, StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [StringLength(225)]
    public string? FirstName { get; set; }

    [StringLength(225)]
    public string? LastName { get; set; }

    [EmailAddress, StringLength(500)]
    public string? Email { get; set; }
}

public sealed record CustomerProfileDto(int Id, string? Username, string? FirstName, string? LastName, string? Email, string? Phone, bool ProfileRequired);
public sealed record CustomerAddressDto(int Id, string? FirstName, string? LastName, int? CountryId, int? StateProvinceId, string? City, string? Address1, string? Address2, string? ZipPostalCode, string? PhoneNumber);
