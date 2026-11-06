using MassTransit;
using Shared.Contracts.Orders;

namespace Dispatch.Infrastructure.Sagas;

public sealed class AssignCourierSaga : MassTransitStateMachine<AssignCourierState>
{
    public AssignCourierSaga(TimeProvider timeProvider)
    {
        InstanceState(saga => saga.CurrentState);
        Event(() => OrderCreated, configuration =>
        {
            configuration.CorrelateById(context => context.Message.OrderId);
            // Claim the OrderId before invoking the selector when initial deliveries race.
            configuration.InsertOnInitial = true;
            configuration.SetSagaFactory(context => new AssignCourierState { CorrelationId = context.Message.OrderId });
        });

        Initially(
            When(OrderCreated)
                .Then(context =>
                {
                    if (context.Message.OrderId == Guid.Empty || context.MessageId is not { } messageId || messageId == Guid.Empty)
                    {
                        throw new InvalidOperationException("OrderCreated requires non-empty OrderId and MessageId.");
                    }

                    context.Saga.InitialMessageId = messageId;
                    context.Saga.PickupLatitude = context.Message.FromLatitude;
                    context.Saga.PickupLongitude = context.Message.FromLongitude;
                    context.Saga.OrderCreatedAt = context.Message.CreatedAt;
                    context.Saga.StartedAt = timeProvider.GetUtcNow();
                })
                .TransitionTo(Searching)
                .Activity(activity => activity.OfType<AssignCourierActivity>())
                .TransitionTo(Assigned));

        // Keep Assigned instances in PostgreSQL: finalizing/deleting would allow a replay to assign again.
        During(Assigned, Ignore(OrderCreated));
    }

    public State Searching { get; private set; } = null!;
    public State Assigned { get; private set; } = null!;
    public Event<OrderCreated> OrderCreated { get; private set; } = null!;
}
