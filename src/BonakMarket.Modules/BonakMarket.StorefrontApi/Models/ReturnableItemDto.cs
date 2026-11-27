using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ReturnableItemDto(int OrderItemId, int ProductId, int MaxQuantity);
