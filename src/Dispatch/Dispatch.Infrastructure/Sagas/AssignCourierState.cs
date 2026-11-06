using MassTransit;

namespace Dispatch.Infrastructure.Sagas;

public sealed class AssignCourierState : SagaStateMachineInstance
{
    // The saga identity is the OrderId, independent of the delivery's MessageId.
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public Guid InitialMessageId { get; set; }
    public double PickupLatitude { get; set; }
    public double PickupLongitude { get; set; }
    public DateTimeOffset OrderCreatedAt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public Guid? CourierId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
}
