namespace Orders.Domain.Orders;

public enum OrderStatus
{
    Created,
    CourierSearching,
    Assigned,
    PickedUp,
    Delivered,
    NoCourierFound,
    Cancelled
}
