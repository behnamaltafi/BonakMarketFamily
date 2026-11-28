#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.Identity.Models;

/// <summary>Search, filter and pagination inputs for public location collections.</summary>
public sealed class LocationListRequest
{
    [Range(1, 10000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [StringLength(200)] public string? Search { get; set; }
    /// <summary>Optional exact location identifier (province or city).</summary>
    [Range(1, int.MaxValue)] public int? Id { get; set; }
}
