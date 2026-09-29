using Orders.Application.Orders.GetOrder;
using Orders.Domain.Orders;
using Orders.Tests.Application.Fakes;

namespace Orders.Tests.Application.GetOrder;

public class GetOrderQueryHandlerTests
{
    private readonly FakeOrderRepository _repository = new();
    private readonly GetOrderQueryHandler _handler;

    public GetOrderQueryHandlerTests()
    {
        _handler = new GetOrderQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_ExistingOrder_ReturnsDto()
    {
        var order = Order.Create(GeoPoint.Create(55.75, 37.62), GeoPoint.Create(55.76, 37.63), 30000, 25);
        _repository.Add(order);

        var result = await _handler.Handle(new GetOrderQuery(order.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(order.Id, result.Id);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNull()
    {
        var result = await _handler.Handle(new GetOrderQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
