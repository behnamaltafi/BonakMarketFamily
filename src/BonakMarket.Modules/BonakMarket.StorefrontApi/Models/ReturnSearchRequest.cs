using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Filters the customer's return requests; Search is an exact request ID.</summary>
public sealed class ReturnSearchRequest : DatedSearchPageRequest
{
    /// <summary>Native return case status identifier.</summary>
    [Range(1, int.MaxValue)] public int? StatusId { get; set; }
}
