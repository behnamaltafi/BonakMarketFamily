using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Filters visible CMS pages by localized title or system name.</summary>
public sealed class TopicSearchRequest : SearchPageRequest
{
    /// <summary>Exact system name of the content page.</summary>
    [StringLength(StorefrontLimits.MaxSearchLength)] public string? SystemName { get; set; }
}
