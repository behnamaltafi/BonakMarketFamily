#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ProductDetailVariantDto(int Id, string Name, int ControlTypeId, bool Required,
    string? Prompt, bool SupportsSelection, IReadOnlyList<ProductDetailVariantValueDto> Values);
