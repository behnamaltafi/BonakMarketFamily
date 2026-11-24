using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Domain;

namespace BonakMarket.Identity.Domain;

[Table("MultiSmsMobileApiSession")]
[Index(nameof(SessionId), IsUnique = true)]
[Index(nameof(CustomerId))]
public sealed class MobileApiSession : BaseEntity
{
    public Guid SessionId { get; set; }
    public int CustomerId { get; set; }
    public Guid AccessId { get; set; }
    [Required, MaxLength(64)]
    public string RefreshHash { get; set; }
    [MaxLength(64)]
    public string PreviousRefreshHash { get; set; }
    [Required, MaxLength(16)]
    public string VerifiedPhone { get; set; }
    [MaxLength(256)]
    public string SecurityStamp { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
    public DateTime? RevokedOnUtc { get; set; }
}
