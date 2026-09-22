namespace Orders.Domain.Orders;

public sealed class Order
{
    public Guid Id { get; private set; }
    public GeoPoint From { get; private set; }
    public GeoPoint To { get; private set; }
    public OrderStatus Status { get; private set; }
    public Guid? CourierId { get; private set; }
    public long PriceMinor { get; private set; }
    public int EtaMinutes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Order() { }

    public static Order Create(GeoPoint from, GeoPoint to, long priceMinor, int etaMinutes)
    {
        if (priceMinor < 0)
        {
            throw new DomainException("Price must not be negative.");
        }

        if (etaMinutes <= 0)
        {
            throw new DomainException("ETA must be positive.");
        }

        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            From = from,
            To = to,
            Status = OrderStatus.Created,
            PriceMinor = priceMinor,
            EtaMinutes = etaMinutes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return order;
    }

    public void StartCourierSearch() =>
        TransitionTo(OrderStatus.CourierSearching, from: OrderStatus.Created);

    public void AssignCourier(Guid courierId)
    {
        if (courierId == Guid.Empty)
        {
            throw new DomainException("Courier id must not be empty.");
        }

        TransitionTo(OrderStatus.Assigned, from: OrderStatus.CourierSearching);
        CourierId = courierId;
    }

    public void MarkNoCourierFound() =>
        TransitionTo(OrderStatus.NoCourierFound, from: OrderStatus.CourierSearching);

    public void MarkPickedUp() =>
        TransitionTo(OrderStatus.PickedUp, from: OrderStatus.Assigned);

    public void MarkDelivered() =>
        TransitionTo(OrderStatus.Delivered, from: OrderStatus.PickedUp);

    public void Cancel() =>
        TransitionTo(OrderStatus.Cancelled,
            from: [OrderStatus.Created, OrderStatus.CourierSearching, OrderStatus.Assigned]);

    private void TransitionTo(OrderStatus target, params ReadOnlySpan<OrderStatus> from)
    {
        if (!from.Contains(Status))
        {
            throw new DomainException($"Cannot transition from {Status} to {target}.");
        }

        Status = target;
    }
}
