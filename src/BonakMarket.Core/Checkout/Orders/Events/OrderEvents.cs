using BonakMarket.Events;

namespace BonakMarket.Core.Checkout.Orders.Events;

public class OrderPaidEvent(Order order) : IEventMessage
{
    public Order Order { get; init; } = Guard.NotNull(order);
}

public class OrderPlacedEvent(Order order) : IEventMessage
{
    public Order Order { get; init; } = Guard.NotNull(order);
}

public class OrderUpdatedEvent(Order order) : IEventMessage
{
    public Order Order { get; init; } = Guard.NotNull(order);
}