using MediatR;

namespace Orders.Application.Orders.GetOrder;

public sealed record GetOrderQuery(Guid Id) : IRequest<OrderDto?>;
