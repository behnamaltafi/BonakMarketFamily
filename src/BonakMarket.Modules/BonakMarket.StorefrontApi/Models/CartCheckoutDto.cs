#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CartCheckoutDto(bool CanStart, bool ShippingRequired, IReadOnlyList<string> Blockers);
