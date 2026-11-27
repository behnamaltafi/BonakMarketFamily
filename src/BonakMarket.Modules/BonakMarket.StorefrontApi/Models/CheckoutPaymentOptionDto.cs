#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CheckoutPaymentOptionDto(string SystemName, string Name, string? Description,
    bool RequiresInteraction, bool RequiresConfirmation, bool RequiresPaymentSelection);
