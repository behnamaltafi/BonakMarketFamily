#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record MyReviewDto(int Id, int ProductId, string? CurrentProductName, string Title,
    string ReviewText, int Rating, bool Approved, DateTime CreatedOnUtc);
