#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed record InstantSearchProductDto(int Id, string Name, string? Sku, int? MediaFileId, string? ImageUrl);
