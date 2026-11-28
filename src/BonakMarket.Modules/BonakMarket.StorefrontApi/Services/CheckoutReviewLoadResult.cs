#nullable enable
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Services;

internal sealed record CheckoutReviewLoadResult(CheckoutDraft? Draft = null, CheckoutReviewDto? Review = null, StorefrontFailure? Error = null);
