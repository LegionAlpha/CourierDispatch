using Dispatch.Application.Abstractions;
using Dispatch.Domain.Delivery;
using MassTransit;
using Shared.Contracts.Orders;
using Shared.Contracts.Dispatch;

namespace Dispatch.Infrastructure.Sagas;

internal sealed class AssignCourierActivity(ICourierSelector selector, TimeProvider timeProvider, IOutboxWriter outbox)
    : IStateMachineActivity<AssignCourierState, OrderCreated>
{
    public async Task Execute(BehaviorContext<AssignCourierState, OrderCreated> context,
        IBehavior<AssignCourierState, OrderCreated> next)
    {
        var pickup = GeoPoint.Create(context.Saga.PickupLatitude, context.Saga.PickupLongitude);
        var courierId = await selector.SelectAsync(context.Saga.CorrelationId, pickup, context.CancellationToken);
        if (courierId == Guid.Empty)
        {
            throw new InvalidOperationException("Courier selection returned an empty courier ID.");
        }

        context.Saga.CourierId = courierId;
        var assignedAt = timeProvider.GetUtcNow();
        context.Saga.AssignedAt = assignedAt;
        outbox.Add(new CourierAssigned(context.Saga.CorrelationId, courierId, assignedAt));
        await next.Execute(context);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<AssignCourierState, OrderCreated, TException> context,
        IBehavior<AssignCourierState, OrderCreated> next) where TException : Exception => next.Faulted(context);

    public void Probe(ProbeContext context) => context.CreateFilterScope("assign-courier");
    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);
}
