using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Core.Common;

/// <summary>A selectable city belonging to a native state/province.</summary>
public class City : BaseEntity, IDisplayOrder
{
    /// <summary>Parent province identifier.</summary>
    public int StateProvinceId { get; set; }

    /// <summary>City display name.</summary>
    [Required, StringLength(100)]
    public string Name { get; set; }

    /// <summary>Whether the city can be selected for new addresses.</summary>
    public bool Published { get; set; }

    /// <summary>Sort order in province city lists.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Native province.</summary>
    public StateProvince StateProvince { get; set; }
}
