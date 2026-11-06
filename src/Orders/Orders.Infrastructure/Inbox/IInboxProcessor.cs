namespace Orders.Infrastructure.Inbox;

internal interface IInboxProcessor
{
    Task<bool> ProcessAsync(Guid messageId, string consumer,
        Func<CancellationToken, Task> handle, CancellationToken cancellationToken);
}
