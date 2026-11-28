using System.ComponentModel.DataAnnotations;

#nullable enable

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Search with an optional UTC creation time interval.</summary>
public class DatedSearchPageRequest : SearchPageRequest, IValidatableObject
{
    /// <summary>Inclusive lower bound for creation time; offsets are converted to UTC.</summary>
    public DateTimeOffset? FromDateUtc { get; set; }

    /// <summary>Exclusive upper bound for creation time; offsets are converted to UTC.</summary>
    public DateTimeOffset? ToDateUtc { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromDateUtc.HasValue && ToDateUtc.HasValue && FromDateUtc.Value >= ToDateUtc.Value)
        {
            yield return new ValidationResult("FromDateUtc must be earlier than ToDateUtc.", new[] { nameof(FromDateUtc), nameof(ToDateUtc) });
        }
    }
}
