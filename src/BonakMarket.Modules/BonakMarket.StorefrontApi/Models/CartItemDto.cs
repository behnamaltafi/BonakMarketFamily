#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CartItemDto(int Id, int ProductId, int Quantity, bool Active, int? ParentItemId);
