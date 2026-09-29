using MediatR;
using Orders.Application.Abstractions;

namespace Orders.Application.Orders.GetOrder;

internal sealed class GetOrderQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetOrderQuery, OrderDto?>
{
    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.Id, cancellationToken);

        return order?.ToDto();
    }
}
