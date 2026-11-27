#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CartVariantDto(int VariantAttributeId, string? AttributeName, int ValueId, string ValueName);
