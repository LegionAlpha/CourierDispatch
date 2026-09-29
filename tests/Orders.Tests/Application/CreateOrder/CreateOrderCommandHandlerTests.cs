using Microsoft.Extensions.Time.Testing;
using Orders.Application.Abstractions;
using Orders.Application.Orders.CreateOrder;
using Orders.Domain.Orders;
using Orders.Tests.Application.Fakes;

namespace Orders.Tests.Application.CreateOrder;

public class CreateOrderCommandHandlerTests
{
    private static readonly CreateOrderCommand _command = new(55.75, 37.62, 55.76, 37.63);
    private static readonly DateTimeOffset _now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeOrderRepository _repository = new();
    private readonly SpyUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(_now);
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        var estimator = new StubDeliveryEstimator(new DeliveryEstimate(PriceMinor: 45000, EtaMinutes: 30));
        _handler = new CreateOrderCommandHandler(estimator, _repository, _unitOfWork, _timeProvider);
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsOrderAndSavesOnce()
    {
        var result = await _handler.Handle(_command, CancellationToken.None);

        var order = Assert.Single(_repository.Orders);
        Assert.Equal(order.Id, result.Id);
        Assert.Equal(1, _unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_ValidCommand_TakesPriceAndEtaFromEstimator()
    {
        var result = await _handler.Handle(_command, CancellationToken.None);

        Assert.Equal(45000, result.PriceMinor);
        Assert.Equal(30, result.EtaMinutes);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCreatedOrderWithCoordinates()
    {
        var result = await _handler.Handle(_command, CancellationToken.None);

        Assert.Equal(OrderStatus.Created, result.Status);
        Assert.Equal(_command.FromLatitude, result.FromLatitude);
        Assert.Equal(_command.FromLongitude, result.FromLongitude);
        Assert.Equal(_command.ToLatitude, result.ToLatitude);
        Assert.Equal(_command.ToLongitude, result.ToLongitude);
        Assert.Null(result.CourierId);
    }

    [Fact]
    public async Task Handle_ValidCommand_TakesCreatedAtFromTimeProvider()
    {
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var result = await _handler.Handle(_command, CancellationToken.None);

        Assert.Equal(_now.AddMinutes(5), result.CreatedAt);
    }
}
