using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ShipmentDto(int Id, int OrderId, string TrackingNumber, string TrackingUrl,
    DateTime? ShippedDateUtc, DateTime? DeliveryDateUtc);
public sealed record ShipmentLineDto(int OrderItemId, int Quantity);
public sealed record ShipmentDetailDto(ShipmentDto Shipment, List<ShipmentLineDto> Items);
public sealed record RecurringPaymentDto(int Id, int InitialOrderId, int CycleLength, int CyclePeriodId,
    int TotalCycles, DateTime StartDateUtc, bool IsActive);
public sealed record ReturnCaseDto(int Id, int OrderItemId, int Quantity, string ReasonForReturn,
    string RequestedAction, string CustomerComments, int StatusId, DateTime CreatedOnUtc);
public sealed record ReturnableItemDto(int OrderItemId, int ProductId, int MaxQuantity);
public sealed record ReturnOptionsDto(bool Allowed, string[] Reasons, string[] Actions, List<ReturnableItemDto> Items);
public sealed class ReturnRequest
{
    [Required, StringLength(4000)] public string Reason { get; set; }
    [Required, StringLength(4000)] public string Action { get; set; }
    [StringLength(4000)] public string Comments { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<ReturnQuantity> Items { get; set; }
}
public sealed class ReturnQuantity
{
    [Range(1, int.MaxValue)] public int OrderItemId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}
public sealed record DownloadableItemDto(Guid OrderItemGuid, int OrderId, int ProductId, string ProductName,
    bool DownloadAllowed, bool LicenseAllowed, bool AgreementRequired, int DownloadCount);
public sealed record DownloadVersionDto(string Version, string FileName, string Changelog);
public sealed record DownloadDetailsDto(DownloadableItemDto Item, List<DownloadVersionDto> Versions, string AgreementText);
