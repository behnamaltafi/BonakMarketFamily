using BonakMarket.Core.Checkout.Affiliates;
using BonakMarket.Core.Checkout.Attributes;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.GiftCards;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Checkout.Tax;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OrderNote> OrderNotes { get; set; }
    public DbSet<TaxCategory> TaxCategories { get; set; }
    public DbSet<GiftCard> GiftCards { get; set; }
    public DbSet<GiftCardUsageHistory> GiftCardUsageHistory { get; set; }
    public DbSet<Affiliate> Affiliates { get; set; }
    public DbSet<CheckoutAttribute> CheckoutAttributes { get; set; }
    public DbSet<CheckoutAttributeValue> CheckoutAttributeValues { get; set; }
    public DbSet<ShoppingCartItem> ShoppingCartItems { get; set; }
    public DbSet<Shipment> Shipments { get; set; }
    public DbSet<ShipmentItem> ShipmentItems { get; set; }
    public DbSet<ShippingMethod> ShippingMethods { get; set; }
    public DbSet<ReturnCase> ReturnCases { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<RecurringPayment> RecurringPayments { get; set; }
    public DbSet<RecurringPaymentHistory> RecurringPaymentHistory { get; set; }
}