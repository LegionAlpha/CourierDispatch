namespace Orders.Infrastructure.Outbox;

internal sealed class OutboxOptions
{
    public int PollIntervalSeconds { get; set; } = 1;
    public int BatchSize { get; set; } = 20;
    public int RetryDelaySeconds { get; set; } = 5;
    public int MaxRetryDelaySeconds { get; set; } = 300;
}
