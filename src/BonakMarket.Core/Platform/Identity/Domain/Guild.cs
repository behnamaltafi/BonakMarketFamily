using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Core.Identity;

[Index(nameof(Code), IsUnique = true, Name = "UX_Guild_Code")]
public class Guild : BonakMarket.Domain.BaseEntity
{
    [Required, StringLength(50)]
    public string Code { get; set; }
    [Required, StringLength(225)]
    public string Title { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}
