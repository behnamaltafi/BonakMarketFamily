#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ProductDetailVariantValueDto(int Id, string Name, bool IsPreselected, string? Color);
