using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record RecurringPaymentDto(int Id, int InitialOrderId, int CycleLength, int CyclePeriodId,
    int TotalCycles, DateTime StartDateUtc, bool IsActive);
