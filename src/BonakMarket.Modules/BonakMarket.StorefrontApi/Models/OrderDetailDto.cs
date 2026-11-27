#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed record OrderDetailDto(OrderDto Order, List<OrderLineDto> Items)
{
    public OrderFinancialsDto Financials { get; init; }
    public OrderAddressDto BillingAddress { get; init; }
    public OrderAddressDto ShippingAddress { get; init; }
    public string ShippingMethod { get; init; }
    public string PaymentMethodSystemName { get; init; }
    public string CustomerComment { get; init; }
    public IReadOnlyList<ShipmentDto> Shipments { get; init; } = [];
    public OrderActionsDto AllowedActions { get; init; }
    public string BrowserInvoiceUrl { get; init; }
    public string BrowserPaymentUrl { get; init; }
}
