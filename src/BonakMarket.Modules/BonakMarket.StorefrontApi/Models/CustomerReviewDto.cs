using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CustomerReviewDto(int Id, string Title, string ReviewText, int Rating, int HelpfulYes, int HelpfulNo, DateTime CreatedOnUtc, bool IsMine);
