namespace Orders.Domain.Orders;

public enum OrderStatus
{
    Created = 0,
    CourierSearching = 10,
    Assigned = 20,
    PickedUp = 30,
    Delivered = 40,
    NoCourierFound = 50,
    Cancelled = 60
}
