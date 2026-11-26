using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Identity.Models;

/// <summary>Additional details for native Customer accounts authenticated by mobile number.</summary>
public sealed class SmsProfileModel
{
    [StringLength(10)]
    public string NationalCode { get; set; }
    public int? GuildId { get; set; }
    public List<GuildOptionDto> Guilds { get; set; } = new();

    [StringLength(225)]
    public string FirstName { get; set; }

    [StringLength(225)]
    public string LastName { get; set; }

    [EmailAddress, StringLength(500)]
    public string Email { get; set; }

    public string Phone { get; set; }
    public string ReturnUrl { get; set; }
}
