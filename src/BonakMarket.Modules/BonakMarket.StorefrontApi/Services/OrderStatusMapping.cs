using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Services;

public static class OrderStatusMapping
{
    public static OrderStatusDto Order(int id) => (OrderStatus)id switch
    {
        OrderStatus.Pending => new(id, "pending", "Pending"),
        OrderStatus.Processing => new(id, "processing", "Processing"),
        OrderStatus.Complete => new(id, "complete", "Complete"),
        OrderStatus.Cancelled => new(id, "cancelled", "Cancelled"),
        _ => new(id, "unknown", "Unknown")
    };

    public static OrderStatusDto Payment(int id) => (PaymentStatus)id switch
    {
        PaymentStatus.Pending => new(id, "pending", "Pending"),
        PaymentStatus.Authorized => new(id, "authorized", "Authorized"),
        PaymentStatus.Paid => new(id, "paid", "Paid"),
        PaymentStatus.PartiallyRefunded => new(id, "partially_refunded", "Partially refunded"),
        PaymentStatus.Refunded => new(id, "refunded", "Refunded"),
        PaymentStatus.Voided => new(id, "voided", "Voided"),
        _ => new(id, "unknown", "Unknown")
    };

    public static OrderStatusDto Shipping(int id) => (ShippingStatus)id switch
    {
        ShippingStatus.ShippingNotRequired => new(id, "not_required", "Shipping not required"),
        ShippingStatus.NotYetShipped => new(id, "not_yet_shipped", "Not yet shipped"),
        ShippingStatus.PartiallyShipped => new(id, "partially_shipped", "Partially shipped"),
        ShippingStatus.Shipped => new(id, "shipped", "Shipped"),
        ShippingStatus.Delivered => new(id, "delivered", "Delivered"),
        _ => new(id, "unknown", "Unknown")
    };
}
