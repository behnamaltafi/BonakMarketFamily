using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Filters reward history by description, date and direction.</summary>
public sealed class RewardPointSearchRequest : DatedSearchPageRequest
{
    /// <summary>True selects positive entries; false selects negative entries; omit for all entries.</summary>
    public bool? IsEarned { get; set; }
}
