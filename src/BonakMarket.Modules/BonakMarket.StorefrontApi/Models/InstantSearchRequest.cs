#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Search-as-you-type inputs. Empty or too-short terms return no suggestions.</summary>
public sealed class InstantSearchRequest
{
    [StringLength(StorefrontLimits.MaxSearchLength)] public string? Search { get; set; }
    [Range(1, StorefrontLimits.MaxSuggestions)] public int? Limit { get; set; }
}
