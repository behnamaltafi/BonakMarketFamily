#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public decimal OrderTotal { get; set; }
    public int OrderStatusId { get; set; }
    public int PaymentStatusId { get; set; }
    public int ShippingStatusId { get; set; }
    public string CustomerCurrencyCode { get; set; }
    public decimal CurrencyRate { get; set; }
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public DateTime? PaidDateUtc { get; set; }
    public OrderStatusDto OrderStatus => OrderStatusMapping.Order(OrderStatusId);
    public OrderStatusDto PaymentStatus => OrderStatusMapping.Payment(PaymentStatusId);
    public OrderStatusDto ShippingStatus => OrderStatusMapping.Shipping(ShippingStatusId);
    public IReadOnlyList<OrderProductPreviewDto> PreviewProducts { get; set; } = [];
}
