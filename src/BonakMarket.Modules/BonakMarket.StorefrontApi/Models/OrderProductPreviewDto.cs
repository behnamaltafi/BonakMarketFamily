#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record OrderProductPreviewDto(int ProductId, string? Name, string? ImageUrl);
