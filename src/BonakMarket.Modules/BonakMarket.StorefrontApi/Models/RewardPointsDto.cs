using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record RewardPointsDto(int Balance, PageDto<RewardPointDto> History);
