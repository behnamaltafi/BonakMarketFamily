#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Capabilities, not a stock reservation or a guarantee the cart will accept a selection.</summary>
public sealed record ProductCapabilitiesDto(bool CanQuote, bool CanAddToCart, bool CanAddToWishlist);
