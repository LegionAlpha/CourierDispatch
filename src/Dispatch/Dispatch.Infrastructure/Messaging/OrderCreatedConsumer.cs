using Dispatch.Infrastructure.Inbox;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Orders;

namespace Dispatch.Infrastructure.Messaging;

internal sealed class OrderCreatedConsumer(
    IInboxProcessor inbox,
    IOrderCreatedHandler handler,
    ILogger<OrderCreatedConsumer> logger) : IConsumer<OrderCreated>
{
    internal const string ConsumerName = "dispatch-order-created";

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        if (context.MessageId is not { } messageId || messageId == Guid.Empty)
        {
            throw new InvalidOperationException("OrderCreated requires a non-empty MessageId for deduplication.");
        }

        var processed = await inbox.ProcessAsync(messageId, ConsumerName,
            cancellationToken => handler.HandleAsync(context.Message, cancellationToken), context.CancellationToken);

        // Log only after commit. Logs are diagnostic, not a transactional business effect.
        if (processed)
        {
            logger.LogInformation("Processed OrderCreated for order {OrderId}, message {MessageId}, correlation {CorrelationId}",
                context.Message.OrderId, messageId, context.CorrelationId);
        }
        else
        {
            logger.LogInformation("Skipped duplicate OrderCreated for order {OrderId}, message {MessageId}",
                context.Message.OrderId, messageId);
        }
    }
}
