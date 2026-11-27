using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Domain;

namespace BonakMarket.StorefrontApi.Domain;

[Table("StorefrontGuestCartToken")]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(CustomerId), IsUnique = true)]
[Index(nameof(ExpiresOnUtc))]
public sealed class GuestCartToken : BaseEntity
{
    [Required, MaxLength(64)]
    public string TokenHash { get; set; }
    public int CustomerId { get; set; }
    public int StoreId { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
    public int? MergedToCustomerId { get; set; }
    public DateTime? MergedOnUtc { get; set; }
    public DateTime? RevokedOnUtc { get; set; }
}
