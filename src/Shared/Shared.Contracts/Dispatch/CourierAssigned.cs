namespace Shared.Contracts.Dispatch;

public sealed record CourierAssigned(Guid OrderId, Guid CourierId, DateTimeOffset AssignedAt);
