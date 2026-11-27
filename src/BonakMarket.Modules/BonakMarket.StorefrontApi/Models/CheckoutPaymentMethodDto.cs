using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record CheckoutPaymentMethodDto(string SystemName, bool RequiresInteraction, bool RequiresConfirmation);
