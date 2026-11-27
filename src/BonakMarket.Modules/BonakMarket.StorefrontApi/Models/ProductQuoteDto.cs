#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ProductQuoteDto(int ProductId, int Quantity, bool IsValid, string? Code,
    ProductQuotePricingDto? Pricing, IReadOnlyList<string> Warnings, bool RequiresCartValidation);
