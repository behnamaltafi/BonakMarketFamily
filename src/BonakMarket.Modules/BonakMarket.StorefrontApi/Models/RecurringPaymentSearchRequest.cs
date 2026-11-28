using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Filters recurring payments by native initial order number and creation time.</summary>
public sealed class RecurringPaymentSearchRequest : DatedSearchPageRequest
{
    /// <summary>Filter by active recurring payment state.</summary>
    public bool? IsActive { get; set; }
}
