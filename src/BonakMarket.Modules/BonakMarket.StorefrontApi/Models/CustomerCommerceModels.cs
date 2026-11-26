using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class UseRewardPointsRequest
{
    public bool Use { get; set; }
}
public sealed record RewardPointDto(int Id, int Points, int PointsBalance, decimal UsedAmount, string Message, DateTime CreatedOnUtc);
public sealed record RewardPointsDto(int Balance, PageDto<RewardPointDto> History);
public sealed record StockSubscriptionDto(int Id, int ProductId, DateTime CreatedOnUtc);
