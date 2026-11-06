using Shared.Contracts.Orders;

namespace Orders.Application.Abstractions;

public interface IOutboxWriter
{
    // Stage the event in the same unit of work as the order; do not save or publish here.
    void Add(OrderCreated message);
}
