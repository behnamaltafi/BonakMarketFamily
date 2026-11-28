using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Filters the customer's stock notifications by product name or creation time.</summary>
public sealed class StockSubscriptionSearchRequest : DatedSearchPageRequest
{
    /// <summary>Filter by product identifier.</summary>
    [Range(1, int.MaxValue)] public int? ProductId { get; set; }
}
