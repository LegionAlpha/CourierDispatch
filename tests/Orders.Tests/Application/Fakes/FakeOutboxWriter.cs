using Orders.Application.Abstractions;
using Shared.Contracts.Orders;

namespace Orders.Tests.Application.Fakes;

internal sealed class FakeOutboxWriter : IOutboxWriter
{
    public List<OrderCreated> Messages { get; } = [];
    public Action<OrderCreated>? OnAdd { get; set; }

    public void Add(OrderCreated message)
    {
        OnAdd?.Invoke(message);
        Messages.Add(message);
    }
}
