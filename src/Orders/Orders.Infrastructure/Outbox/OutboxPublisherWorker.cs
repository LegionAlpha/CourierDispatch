using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orders.Infrastructure.Outbox;

internal sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    for (var i = 0; i < options.Value.BatchSize; i++)
                    {
                        // Each attempt gets a fresh DbContext, also after database failures.
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();
                        if (!await processor.ProcessNextAsync(stoppingToken))
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Outbox processing failed; the next poll will retry with a new scope");
                }

                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown; an uncommitted message stays pending.
        }
    }
}
