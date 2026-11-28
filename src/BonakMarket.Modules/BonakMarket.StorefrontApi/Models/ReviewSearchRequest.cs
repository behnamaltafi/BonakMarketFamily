using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Filters product reviews by rating, title or review text.</summary>
public class ReviewSearchRequest : SearchPageRequest
{
    /// <summary>Filter by rating from one to five stars.</summary>
    [Range(1, 5)] public int? Rating { get; set; }
}
