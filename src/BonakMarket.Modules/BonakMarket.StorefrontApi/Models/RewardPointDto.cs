using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record RewardPointDto(int Id, int Points, int PointsBalance, decimal UsedAmount, string Message, DateTime CreatedOnUtc);
