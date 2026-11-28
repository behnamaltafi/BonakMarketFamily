using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Core.Common;

/// <summary>A selectable city belonging to a native state/province.</summary>
public class City : BaseEntity, IDisplayOrder
{
    public int StateProvinceId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; }

    public bool Published { get; set; }

    public int DisplayOrder { get; set; }

    public StateProvince StateProvince { get; set; }
}
