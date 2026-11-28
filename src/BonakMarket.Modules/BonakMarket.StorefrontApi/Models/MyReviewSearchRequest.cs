using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Filters the customer's own reviews; Search also matches the product name.</summary>
public sealed class MyReviewSearchRequest : ReviewSearchRequest
{
    /// <summary>Filter by product identifier.</summary>
    [Range(1, int.MaxValue)] public int? ProductId { get; set; }

    /// <summary>Filter by approval state.</summary>
    public bool? IsApproved { get; set; }
}
