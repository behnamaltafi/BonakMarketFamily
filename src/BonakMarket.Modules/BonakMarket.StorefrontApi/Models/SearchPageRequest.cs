using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Shared search and pagination contract for storefront lists.</summary>
public class SearchPageRequest : PageRequest
{
    /// <summary>Optional search term; its meaning depends on the collection.</summary>
    [StringLength(StorefrontLimits.MaxSearchLength)] public string? Search { get; set; }
}
