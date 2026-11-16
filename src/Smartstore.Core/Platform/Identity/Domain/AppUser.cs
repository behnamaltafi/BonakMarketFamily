using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Smartstore.Core.Identity;

/// <summary>
/// Represents an application user independently of any commerce customer.
/// </summary>
[Index(nameof(UserGuid), IsUnique = true, Name = "UX_AppUser_UserGuid")]
public partial class AppUser : BaseEntity
{
    /// <summary>
    /// Stable public identifier; it is not a customer identifier.
    /// </summary>
    public Guid UserGuid { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Optional issuer-qualified subject of an external identity provider.
    /// </summary>
    [StringLength(450)]
    public string ExternalSubject { get; set; }

    public bool Active { get; set; } = true;

    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
}
