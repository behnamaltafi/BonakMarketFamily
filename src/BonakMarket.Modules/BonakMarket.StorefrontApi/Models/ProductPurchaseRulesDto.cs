#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ProductPurchaseRulesDto(int MinimumQuantity, int MaximumQuantity,
    int QuantityStep, IReadOnlyList<int> AllowedQuantities, bool HideQuantityControl, bool RequiresConfiguration);
