using MediatR;

namespace Orders.Application.Orders.CreateOrder;

public sealed record CreateOrderCommand(
    double FromLatitude,
    double FromLongitude,
    double ToLatitude,
    double ToLongitude) : IRequest<OrderDto>;
