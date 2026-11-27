#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CartLineActionsDto(bool CanUpdateQuantity, bool CanRemove, bool CanTransfer);
