using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Options;
using Orders.Infrastructure.Outbox;
using Shared.Contracts.Orders;

namespace Orders.Infrastructure.Messaging;

internal sealed class MassTransitOutboxPublisher(
    IPublishEndpoint publishEndpoint,
    IOptions<RabbitMqOptions> options) : IOutboxMessagePublisher
{
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (message.Type != OutboxWriter.OrderCreatedType)
        {
            throw new InvalidOperationException($"Unknown outbox message type '{message.Type}'.");
        }

        var orderCreated = JsonSerializer.Deserialize<OrderCreated>(message.Payload, OutboxWriter.SerializerOptions)
            ?? throw new JsonException("Outbox payload is null.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.PublishTimeoutSeconds));

        await publishEndpoint.Publish(orderCreated, context =>
        {
            // A retry must retain the same identity, including after a process restart.
            context.MessageId = message.Id;
            context.CorrelationId = orderCreated.OrderId;
        }, timeout.Token);
    }
}
