using MassTransit;
using Microsoft.Extensions.Logging;
using Orders.Infrastructure.Inbox;
using Shared.Contracts;
using Shared.Contracts.Dispatch;

namespace Orders.Infrastructure.Messaging;

internal sealed class CourierAssignedConsumer(
    IInboxProcessor inbox, ICourierAssignedHandler handler, ILogger<CourierAssignedConsumer> logger) : IConsumer<CourierAssigned>
{
    public async Task Consume(ConsumeContext<CourierAssigned> context)
    {
        if (context.MessageId is not { } messageId || messageId == Guid.Empty
            || context.Message.OrderId == Guid.Empty || context.Message.CourierId == Guid.Empty)
        {
            throw new InvalidOperationException("CourierAssigned requires non-empty MessageId, OrderId and CourierId.");
        }

        var processed = await inbox.ProcessAsync(messageId, QueueNames.OrdersCourierAssigned,
            token => handler.HandleAsync(context.Message, token), context.CancellationToken);
        if (processed)
        {
            logger.LogInformation("Processed CourierAssigned for order {OrderId}, courier {CourierId}, message {MessageId}",
                context.Message.OrderId, context.Message.CourierId, messageId);
        }
        else
        {
            logger.LogInformation("Skipped duplicate CourierAssigned for order {OrderId}, message {MessageId}",
                context.Message.OrderId, messageId);
        }
    }
}
