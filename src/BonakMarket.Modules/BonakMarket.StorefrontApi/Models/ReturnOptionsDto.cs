using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ReturnOptionsDto(bool Allowed, string[] Reasons, string[] Actions, List<ReturnableItemDto> Items);
