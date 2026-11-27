using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ReturnCaseDto(int Id, int OrderItemId, int Quantity, string ReasonForReturn,
    string RequestedAction, string CustomerComments, int StatusId, DateTime CreatedOnUtc);
