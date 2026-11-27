#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CheckoutReviewPreparedDto(Guid SessionId, int Revision);
