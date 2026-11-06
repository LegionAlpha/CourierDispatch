using Dispatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Inbox;

internal sealed class InboxProcessor(DispatchDbContext dbContext, TimeProvider timeProvider) : IInboxProcessor
{
    public async Task<bool> ProcessAsync(Guid messageId, string consumer,
        Func<CancellationToken, Task> handle, CancellationToken cancellationToken)
    {
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("Inbox requires a non-empty MessageId.", nameof(messageId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var receivedAt = timeProvider.GetUtcNow();
        // The unique key serializes concurrent copies, even across service instances.
        // This uncommitted claim disappears if handling or committing fails.
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO inbox_messages (message_id, consumer, received_at)
            VALUES ({messageId}, {consumer}, {receivedAt})
            ON CONFLICT (message_id, consumer) DO NOTHING
            """, cancellationToken);
        if (inserted == 0)
        {
            return false;
        }

        // The handler must use this scoped DbContext; external side effects need an outbox.
        await handle(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var processedAt = timeProvider.GetUtcNow();
        await dbContext.InboxMessages
            .Where(message => message.MessageId == messageId && message.Consumer == consumer)
            .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.ProcessedAt, processedAt),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
