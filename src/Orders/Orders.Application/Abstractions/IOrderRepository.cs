using Orders.Domain.Orders;

namespace Orders.Application.Abstractions;

public interface IOrderRepository
{
    void Add(Order order);

    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
