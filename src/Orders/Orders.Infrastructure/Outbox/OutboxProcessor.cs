using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure.Outbox;

internal sealed class OutboxProcessor(
    OrdersDbContext dbContext,
    IOutboxMessagePublisher publisher,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        // Keep one row locked until publication and acknowledgement are saved.
        // Other workers skip this row instead of publishing it concurrently.
        var messages = await dbContext.OutboxMessages.FromSql($"""
            SELECT * FROM outbox_messages
            WHERE processed_at IS NULL AND next_attempt_at <= {now}
            ORDER BY next_attempt_at, occurred_at, id
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        var message = messages.SingleOrDefault();
        if (message is null)
        {
            return false;
        }

        message.Attempts++;
        try
        {
            await publisher.PublishAsync(message, cancellationToken);
            message.ProcessedAt = timeProvider.GetUtcNow();
            message.LastError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var delaySeconds = Math.Min(options.Value.MaxRetryDelaySeconds,
                options.Value.RetryDelaySeconds * Math.Pow(2, Math.Min(message.Attempts - 1, 30)));
            message.NextAttemptAt = timeProvider.GetUtcNow().AddSeconds(delaySeconds);
            var error = $"{exception.GetType().Name}: {exception.Message}";
            message.LastError = error[..Math.Min(error.Length, 2000)];
            logger.LogWarning(exception,
                "Outbox message {MessageId} attempt {Attempt} failed; retry at {NextAttemptAt}",
                message.Id, message.Attempts, message.NextAttemptAt);
        }

        // If saving/committing fails after publish, the pending row will be retried.
        // This is at-least-once delivery: a consumer must tolerate duplicate MessageIds.
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (message.ProcessedAt is not null)
        {
            logger.LogInformation("Published outbox message {MessageId} of type {MessageType}", message.Id, message.Type);
        }

        return true;
    }
}
