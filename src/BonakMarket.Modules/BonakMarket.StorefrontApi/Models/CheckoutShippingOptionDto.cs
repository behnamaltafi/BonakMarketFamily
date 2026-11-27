using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CheckoutShippingOptionDto(int Id, string Provider, string Name, string Description, decimal Rate);
