using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CheckoutReadinessDto(bool CanStart, string[] Blockers, bool ShippingRequired,
    int? DefaultBillingAddressId, int? DefaultShippingAddressId, string BrowserCheckoutUrl, bool RequiresBrowserSession);
