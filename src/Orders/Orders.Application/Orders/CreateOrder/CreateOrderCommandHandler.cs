using MediatR;
using Orders.Application.Abstractions;
using Orders.Domain.Orders;
using Shared.Contracts.Orders;

namespace Orders.Application.Orders.CreateOrder;

internal sealed class CreateOrderCommandHandler(
    IDeliveryEstimator deliveryEstimator,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    IOutboxWriter outbox,
    TimeProvider timeProvider) : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var from = GeoPoint.Create(request.FromLatitude, request.FromLongitude);
        var to = GeoPoint.Create(request.ToLatitude, request.ToLongitude);

        var estimate = await deliveryEstimator.EstimateAsync(from, to, cancellationToken);

        var order = Order.Create(from, to, estimate.PriceMinor, estimate.EtaMinutes, timeProvider.GetUtcNow());

        orderRepository.Add(order);
        outbox.Add(new OrderCreated(
            order.Id,
            order.From.Latitude,
            order.From.Longitude,
            order.To.Latitude,
            order.To.Longitude,
            order.PriceMinor,
            order.EtaMinutes,
            order.CreatedAt));

        // The order and its event are committed together by the same DbContext.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }
}
