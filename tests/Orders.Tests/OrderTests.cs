using Orders.Domain;
using Orders.Domain.Orders;

namespace Orders.Tests;

public class OrderTests
{
    private static readonly GeoPoint From = new(55.75, 37.62);
    private static readonly GeoPoint To = new(55.76, 37.63);

    // Create

    [Fact]
    public void Create_ReturnsOrderInCreatedStatus()
    {
        var order = Order.Create(From, To, priceMinor: 50000, etaMinutes: 20);

        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Null(order.CourierId);
        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(From, order.From);
        Assert.Equal(To, order.To);
        Assert.Equal(50000, order.PriceMinor);
        Assert.Equal(20, order.EtaMinutes);
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(50000, 0)]
    [InlineData(50000, -5)]
    public void Create_WithInvalidPriceOrEta_Throws(long priceMinor, int etaMinutes)
    {
        Assert.Throws<DomainException>(() => Order.Create(From, To, priceMinor, etaMinutes));
    }

    [Fact]
    public void Create_WithZeroPrice_Succeeds()
    {
        var order = Order.Create(From, To, priceMinor: 0, etaMinutes: 1);

        Assert.Equal(0, order.PriceMinor);
    }

    [Fact]
    public void Create_GeneratesSequentialIds()
    {
        var first = Order.Create(From, To, 50000, 20);
        var second = Order.Create(From, To, 50000, 20);

        Assert.True(second.Id.CompareTo(first.Id) > 0);
    }

    // Transition

    [Fact]
    public void StartCourierSearch_FromCreated_SetsCourierSearching()
    {
        var order = CreateOrder();

        order.StartCourierSearch();

        Assert.Equal(OrderStatus.CourierSearching, order.Status);
    }

    [Fact]
    public void AssignCourier_FromCourierSearching_SetsAssignedAndCourierId()
    {
        var order = CreateSearchingOrder();
        var courierId = Guid.NewGuid();

        order.AssignCourier(courierId);

        Assert.Equal(OrderStatus.Assigned, order.Status);
        Assert.Equal(courierId, order.CourierId);
    }

    [Fact]
    public void MarkNoCourierFound_FromCourierSearching_SetsNoCourierFound()
    {
        var order = CreateSearchingOrder();

        order.MarkNoCourierFound();

        Assert.Equal(OrderStatus.NoCourierFound, order.Status);
        Assert.Null(order.CourierId);
    }

    [Fact]
    public void MarkPickedUp_FromAssigned_SetsPickedUp()
    {
        var order = CreateAssignedOrder();

        order.MarkPickedUp();

        Assert.Equal(OrderStatus.PickedUp, order.Status);
    }

    [Fact]
    public void MarkDelivered_FromPickedUp_SetsDelivered()
    {
        var order = CreatePickedUpOrder();

        order.MarkDelivered();

        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void FullHappyPath_EndsInDelivered()
    {
        var order = CreateOrder();
        var courierId = Guid.NewGuid();

        order.StartCourierSearch();
        order.AssignCourier(courierId);
        order.MarkPickedUp();
        order.MarkDelivered();

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(courierId, order.CourierId);
    }

    // Cancel

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.CourierSearching)]
    [InlineData(OrderStatus.Assigned)]
    public void Cancel_FromCancellableStatus_SetsCancelled(OrderStatus initial)
    {
        var order = CreateOrderIn(initial);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.NoCourierFound)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancel_FromNonCancellableStatus_Throws(OrderStatus initial)
    {
        var order = CreateOrderIn(initial);

        var ex = Assert.Throws<DomainException>(() => order.Cancel());

        Assert.Contains("Cannot transition", ex.Message);
        Assert.Equal(initial, order.Status);
    }

    // Forbidden Transitions
    public static TheoryData<OrderStatus, string, Action<Order>> ForbiddenTransitions => new()
    {
        { OrderStatus.Created,          nameof(Order.AssignCourier),      o => o.AssignCourier(Guid.NewGuid()) },
        { OrderStatus.Created,          nameof(Order.MarkNoCourierFound), o => o.MarkNoCourierFound() },
        { OrderStatus.Created,          nameof(Order.MarkPickedUp),       o => o.MarkPickedUp() },
        { OrderStatus.Created,          nameof(Order.MarkDelivered),      o => o.MarkDelivered() },
        { OrderStatus.CourierSearching, nameof(Order.StartCourierSearch), o => o.StartCourierSearch() },
        { OrderStatus.CourierSearching, nameof(Order.MarkPickedUp),       o => o.MarkPickedUp() },
        { OrderStatus.Assigned,         nameof(Order.AssignCourier),      o => o.AssignCourier(Guid.NewGuid()) },
        { OrderStatus.Assigned,         nameof(Order.MarkDelivered),      o => o.MarkDelivered() },
        { OrderStatus.Delivered,        nameof(Order.MarkPickedUp),       o => o.MarkPickedUp() },
        { OrderStatus.NoCourierFound,   nameof(Order.StartCourierSearch), o => o.StartCourierSearch() },
        { OrderStatus.Cancelled,        nameof(Order.StartCourierSearch), o => o.StartCourierSearch() },
    };

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void ForbiddenTransition_Throws_AndKeepsStatus(OrderStatus initial, string action, Action<Order> act)
    {
        var order = CreateOrderIn(initial);

        var ex = Assert.Throws<DomainException>(() => act(order));

        Assert.Contains("Cannot transition", ex.Message);
        Assert.Equal(initial, order.Status);
        _ = action;
    }

    [Fact]
    public void AssignCourier_WithEmptyGuid_Throws()
    {
        var order = CreateSearchingOrder();

        var ex = Assert.Throws<DomainException>(() => order.AssignCourier(Guid.Empty));

        Assert.Contains("must not be empty", ex.Message);
        Assert.Equal(OrderStatus.CourierSearching, order.Status);
        Assert.Null(order.CourierId);
    }

    // Helpers

    private static Order CreateOrder() => Order.Create(From, To, 50000, 20);

    private static Order CreateSearchingOrder()
    {
        var order = CreateOrder();
        order.StartCourierSearch();
        return order;
    }

    private static Order CreateAssignedOrder()
    {
        var order = CreateSearchingOrder();
        order.AssignCourier(Guid.NewGuid());
        return order;
    }

    private static Order CreatePickedUpOrder()
    {
        var order = CreateAssignedOrder();
        order.MarkPickedUp();
        return order;
    }

    private static Order CreateOrderIn(OrderStatus status)
    {
        switch (status)
        {
            case OrderStatus.Created:
                return CreateOrder();
            case OrderStatus.CourierSearching:
                return CreateSearchingOrder();
            case OrderStatus.Assigned:
                return CreateAssignedOrder();
            case OrderStatus.PickedUp:
                return CreatePickedUpOrder();
            case OrderStatus.Delivered:
            {
                var order = CreatePickedUpOrder();
                order.MarkDelivered();
                return order;
            }
            case OrderStatus.NoCourierFound:
            {
                var order = CreateSearchingOrder();
                order.MarkNoCourierFound();
                return order;
            }
            case OrderStatus.Cancelled:
            {
                var order = CreateOrder();
                order.Cancel();
                return order;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }
}
