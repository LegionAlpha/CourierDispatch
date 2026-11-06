using Shared.Contracts.Orders;

namespace Dispatch.Infrastructure.Messaging;

internal interface IOrderCreatedHandler
{
    Task HandleAsync(OrderCreated message, CancellationToken cancellationToken);
}
