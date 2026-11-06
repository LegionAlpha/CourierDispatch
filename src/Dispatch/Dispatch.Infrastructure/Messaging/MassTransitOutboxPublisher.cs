using System.Text.Json;
using Dispatch.Infrastructure.Outbox;
using MassTransit;
using Microsoft.Extensions.Options;
using Shared.Contracts.Dispatch;

namespace Dispatch.Infrastructure.Messaging;

internal sealed class MassTransitOutboxPublisher(
    IPublishEndpoint publishEndpoint, IOptions<RabbitMqOptions> options) : IOutboxMessagePublisher
{
    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (message.Type != OutboxWriter.CourierAssignedType)
        {
            throw new InvalidOperationException($"Unknown outbox message type '{message.Type}'.");
        }

        var assigned = JsonSerializer.Deserialize<CourierAssigned>(message.Payload, OutboxWriter.SerializerOptions)
            ?? throw new JsonException("Outbox payload is null.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.PublishTimeoutSeconds));
        await publishEndpoint.Publish(assigned, context =>
        {
            context.MessageId = message.Id;
            context.CorrelationId = assigned.OrderId;
        }, timeout.Token);
    }
}
