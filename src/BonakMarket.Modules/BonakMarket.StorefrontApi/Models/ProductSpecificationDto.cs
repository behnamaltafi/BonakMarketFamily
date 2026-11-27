#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ProductSpecificationDto(int AttributeId, string Name, int OptionId, string Value);
