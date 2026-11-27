#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CartTotalDto(decimal? Total, decimal DiscountAmount, string CurrencyCode);
