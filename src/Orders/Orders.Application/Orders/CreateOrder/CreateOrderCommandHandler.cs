using MediatR;
using Orders.Application.Abstractions;
using Orders.Domain.Orders;

namespace Orders.Application.Orders.CreateOrder;

internal sealed class CreateOrderCommandHandler(
    IDeliveryEstimator deliveryEstimator,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var from = GeoPoint.Create(request.FromLatitude, request.FromLongitude);
        var to = GeoPoint.Create(request.ToLatitude, request.ToLongitude);

        var estimate = await deliveryEstimator.EstimateAsync(from, to, cancellationToken);

        var order = Order.Create(from, to, estimate.PriceMinor, estimate.EtaMinutes);

        orderRepository.Add(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return order.ToDto();
    }
}
