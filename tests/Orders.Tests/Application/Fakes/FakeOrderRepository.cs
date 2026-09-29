using Orders.Application.Abstractions;
using Orders.Domain.Orders;

namespace Orders.Tests.Application.Fakes;

internal sealed class FakeOrderRepository : IOrderRepository
{
    public List<Order> Orders { get; } = [];

    public void Add(Order order) => Orders.Add(order);

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.FirstOrDefault(order => order.Id == id));
}
