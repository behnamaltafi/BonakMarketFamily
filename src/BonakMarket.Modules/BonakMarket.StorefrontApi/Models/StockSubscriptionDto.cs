using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record StockSubscriptionDto(int Id, int ProductId, DateTime CreatedOnUtc);
