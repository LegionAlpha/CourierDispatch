using Shared.Contracts.Dispatch;

namespace Orders.Infrastructure.Messaging;

internal interface ICourierAssignedHandler
{
    Task HandleAsync(CourierAssigned message, CancellationToken cancellationToken);
}
