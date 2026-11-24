#nullable enable
using System.ComponentModel.DataAnnotations;
namespace BonakMarket.StorefrontApi.Models;

public sealed class PrepareCheckoutReviewRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
}

public sealed record CheckoutReviewPreparedDto(Guid SessionId, int Revision);

public sealed record CheckoutReviewDto(Guid SessionId, int Revision, string CurrencyCode,
    decimal PayableTotal, string CartFingerprint, bool CanConfirm);

public sealed class ConfirmCheckoutRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    [Required] public decimal? ExpectedOrderTotal { get; set; }
    [Required, StringLength(5)] public string CurrencyCode { get; set; } = "";
    [Required, StringLength(64, MinimumLength = 64)] public string CartFingerprint { get; set; } = "";
    public bool AcceptTerms { get; set; }
}

public sealed record CheckoutConfirmationDto(Guid SessionId, string Status, int? OrderId,
    string? OrderNumber, OrderStatusDto? PaymentStatus, bool RequiresReconciliation);
