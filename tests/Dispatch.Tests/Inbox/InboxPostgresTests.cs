using Dispatch.Infrastructure.Inbox;
using Dispatch.Infrastructure.Messaging;
using Dispatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Shared.Contracts.Orders;

namespace Dispatch.Tests.Inbox;

[Trait("Category", "PostgreSQL")]
public sealed class InboxPostgresTests : IAsyncLifetime
{
    private readonly string _databaseName = $"dispatch_inbox_test_{Guid.NewGuid():N}";
    private string _adminConnection = string.Empty;
    private string _connectionString = string.Empty;
    private bool _databaseCreated;

    public async Task InitializeAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DISPATCH_TEST_POSTGRES")
            ?? throw new InvalidOperationException("DISPATCH_TEST_POSTGRES is required."))
        { Pooling = false };
        _adminConnection = connection.ConnectionString;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();
        _databaseCreated = true;
        connection.Database = _databaseName;
        _connectionString = connection.ConnectionString;
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (!_databaseCreated)
        {
            return;
        }

        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        // The database name belongs to this test instance, never to configured application data.
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task FirstMessage_CommitsInboxAndReceivedOrder()
    {
        var messageId = Guid.NewGuid();
        var message = CreateEvent();
        await using (var db = CreateDb())
        {
            Assert.True(await ProcessAsync(db, messageId, message));
        }

        await using var verification = CreateDb();
        var receipt = await verification.InboxMessages.SingleAsync();
        Assert.Equal(messageId, receipt.MessageId);
        Assert.Equal(OrderCreatedConsumer.ConsumerName, receipt.Consumer);
        Assert.NotNull(receipt.ProcessedAt);
        Assert.True(receipt.ProcessedAt >= receipt.ReceivedAt);
        var order = await verification.ReceivedOrders.SingleAsync();
        Assert.Equal(message.OrderId, order.OrderId);
        Assert.Equal(message.FromLatitude, order.FromLatitude);
        Assert.Equal(message.FromLongitude, order.FromLongitude);
        Assert.Equal(message.ToLatitude, order.ToLatitude);
        Assert.Equal(message.ToLongitude, order.ToLongitude);
        Assert.Equal(message.PriceMinor, order.PriceMinor);
        Assert.Equal(message.EtaMinutes, order.EtaMinutes);
        Assert.Equal(message.CreatedAt, order.CreatedAt);
    }

    [PostgresFact]
    public async Task DuplicateAfterNewContext_DoesNotInvokeHandlerAgain()
    {
        var id = Guid.NewGuid();
        var message = CreateEvent();
        await using (var db = CreateDb())
        {
            Assert.True(await ProcessAsync(db, id, message));
        }
        await using (var db = CreateDb())
        {
            Assert.False(await new InboxProcessor(db, TimeProvider.System).ProcessAsync(id,
                OrderCreatedConsumer.ConsumerName, _ => throw new InvalidOperationException("Must not run twice"),
                CancellationToken.None));
        }

        await using var verification = CreateDb();
        Assert.Single(await verification.InboxMessages.ToListAsync());
        Assert.Single(await verification.ReceivedOrders.ToListAsync());
    }

    [PostgresFact]
    public async Task SameOrderWithNewMessageId_DoesNotDuplicateOrOverwriteOrder()
    {
        var message = CreateEvent();
        await using (var db = CreateDb())
        {
            Assert.True(await ProcessAsync(db, Guid.NewGuid(), message));
        }
        await using (var db = CreateDb())
        {
            Assert.True(await ProcessAsync(db, Guid.NewGuid(), message with { PriceMinor = message.PriceMinor + 100 }));
        }

        await using var verification = CreateDb();
        Assert.Equal(2, await verification.InboxMessages.CountAsync());
        Assert.Equal(message.PriceMinor, (await verification.ReceivedOrders.SingleAsync()).PriceMinor);
    }

    [PostgresFact]
    public async Task HandlerFails_RollsBackReceiptAndOrder_ThenRetrySucceeds()
    {
        var id = Guid.NewGuid();
        var message = CreateEvent();
        await using (var db = CreateDb())
        {
            var handler = new OrderCreatedHandler(db, TimeProvider.System);
            await Assert.ThrowsAsync<IOException>(() => new InboxProcessor(db, TimeProvider.System)
                .ProcessAsync(id, OrderCreatedConsumer.ConsumerName, async token =>
                {
                    await handler.HandleAsync(message, token);
                    throw new IOException("Failure after business write");
                }, CancellationToken.None));
        }
        await AssertEmptyAsync();
        await using var retryDb = CreateDb();
        Assert.True(await ProcessAsync(retryDb, id, message));
    }

    [PostgresFact]
    public async Task SaveFailsAfterHandling_RollsBackBothWrites()
    {
        var id = Guid.NewGuid();
        var message = CreateEvent();
        await using (var db = CreateDb(new FailSaveInterceptor()))
        {
            await Assert.ThrowsAsync<IOException>(() => ProcessAsync(db, id, message));
        }
        await AssertEmptyAsync();
        await using var retryDb = CreateDb();
        Assert.True(await ProcessAsync(retryDb, id, message));
    }

    [PostgresFact]
    public async Task ConcurrentDuplicates_RunHandlerOnce()
    {
        var id = Guid.NewGuid();
        var message = CreateEvent();
        var handlerCalls = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = CreateDb();
            var handler = new OrderCreatedHandler(db, TimeProvider.System);
            return await new InboxProcessor(db, TimeProvider.System).ProcessAsync(id,
                OrderCreatedConsumer.ConsumerName, async token =>
                {
                    Interlocked.Increment(ref handlerCalls);
                    await handler.HandleAsync(message, token);
                    await Task.Delay(100, token);
                }, timeout.Token);
        }));

        Assert.Equal(1, handlerCalls);
        Assert.Equal(1, results.Count(processed => processed));
        await using var verification = CreateDb();
        Assert.Single(await verification.InboxMessages.ToListAsync());
        Assert.Single(await verification.ReceivedOrders.ToListAsync());
    }

    [PostgresFact]
    public async Task CancellationBeforeCommit_RollsBackAndAllowsRetry()
    {
        var id = Guid.NewGuid();
        var message = CreateEvent();
        using var cancellation = new CancellationTokenSource();
        await using (var db = CreateDb())
        {
            var handler = new OrderCreatedHandler(db, TimeProvider.System);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new InboxProcessor(db, TimeProvider.System)
                .ProcessAsync(id, OrderCreatedConsumer.ConsumerName, async token =>
                {
                    await handler.HandleAsync(message, token);
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }, cancellation.Token));
        }
        await AssertEmptyAsync();
        await using var retryDb = CreateDb();
        Assert.True(await ProcessAsync(retryDb, id, message));
    }

    [PostgresFact]
    public async Task DifferentConsumers_CanEachProcessTheSameMessageId()
    {
        var id = Guid.NewGuid();
        await using (var db = CreateDb())
        {
            Assert.True(await new InboxProcessor(db, TimeProvider.System).ProcessAsync(id, "consumer-a",
                _ => Task.CompletedTask, CancellationToken.None));
        }
        await using (var db = CreateDb())
        {
            Assert.True(await new InboxProcessor(db, TimeProvider.System).ProcessAsync(id, "consumer-b",
                _ => Task.CompletedTask, CancellationToken.None));
        }
        await using var verification = CreateDb();
        Assert.Equal(2, await verification.InboxMessages.CountAsync());
    }

    private async Task AssertEmptyAsync()
    {
        await using var db = CreateDb();
        Assert.Empty(await db.InboxMessages.ToListAsync());
        Assert.Empty(await db.ReceivedOrders.ToListAsync());
    }

    private DispatchDbContext CreateDb(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<DispatchDbContext>().UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention().AddInterceptors(interceptors).Options);

    private static Task<bool> ProcessAsync(DispatchDbContext db, Guid messageId, OrderCreated message)
    {
        var handler = new OrderCreatedHandler(db, TimeProvider.System);
        return new InboxProcessor(db, TimeProvider.System).ProcessAsync(messageId, OrderCreatedConsumer.ConsumerName,
            token => handler.HandleAsync(message, token), CancellationToken.None);
    }

    private static OrderCreated CreateEvent() => new(Guid.NewGuid(), 25, 55, 26, 56, 12000, 8,
        new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero));

    private sealed class FailSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new IOException("Database save failed after handling.");
    }
}
