#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CheckoutConfirmationDto(Guid SessionId, string Status, int? OrderId,
    string? OrderNumber, OrderStatusDto? PaymentStatus, bool RequiresReconciliation);
