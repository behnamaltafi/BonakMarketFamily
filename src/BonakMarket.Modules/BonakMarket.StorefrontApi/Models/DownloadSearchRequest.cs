using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Filters downloadable purchases by product name or identifiers.</summary>
public sealed class DownloadSearchRequest : SearchPageRequest
{
    /// <summary>Filter by order identifier.</summary>
    [Range(1, int.MaxValue)] public int? OrderId { get; set; }

    /// <summary>Filter by product identifier.</summary>
    [Range(1, int.MaxValue)] public int? ProductId { get; set; }
}
