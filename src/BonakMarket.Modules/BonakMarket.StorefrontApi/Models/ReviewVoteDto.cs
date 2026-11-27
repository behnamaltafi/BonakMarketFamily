using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ReviewVoteDto(int HelpfulYes, int HelpfulNo);
