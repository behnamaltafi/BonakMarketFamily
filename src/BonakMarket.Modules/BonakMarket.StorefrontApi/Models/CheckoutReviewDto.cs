#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CheckoutReviewDto(Guid SessionId, int Revision, string CurrencyCode,
    decimal PayableTotal, string CartFingerprint, bool CanConfirm);
