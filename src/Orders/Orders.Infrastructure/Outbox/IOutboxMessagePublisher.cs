namespace Orders.Infrastructure.Outbox;

internal interface IOutboxMessagePublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
