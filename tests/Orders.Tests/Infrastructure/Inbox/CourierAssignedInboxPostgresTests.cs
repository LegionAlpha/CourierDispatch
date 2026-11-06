using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Orders.Domain;
using Orders.Domain.Orders;
using Orders.Infrastructure.Inbox;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;
using Orders.Tests.Infrastructure.Outbox;
using Shared.Contracts;
using Shared.Contracts.Dispatch;

namespace Orders.Tests.Infrastructure.Inbox;

[Trait("Category", "PostgreSQL")]
public sealed class CourierAssignedInboxPostgresTests : IAsyncLifetime
{
    private readonly string _databaseName = $"orders_inbox_test_{Guid.NewGuid():N}";
    private string _adminConnection = string.Empty;
    private string _connectionString = string.Empty;
    private bool _databaseCreated;

    public async Task InitializeAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ORDERS_TEST_POSTGRES")
            ?? throw new InvalidOperationException("ORDERS_TEST_POSTGRES is required."))
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
        if (!_databaseCreated) return;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        // Only this test's randomly named database is removed.
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task Assignment_CommitsOrderStatusCourierAndInboxTogether()
    {
        var message = await SeedAsync();
        var id = Guid.NewGuid();
        await using (var db = CreateDb())
        {
            Assert.True(await ProcessAsync(db, id, message));
        }
        await using var verification = CreateDb();
        var order = await verification.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Assigned, order.Status);
        Assert.Equal(message.CourierId, order.CourierId);
        var inbox = await verification.InboxMessages.SingleAsync();
        Assert.Equal(id, inbox.MessageId);
        Assert.NotNull(inbox.ProcessedAt);
    }

    [PostgresFact]
    public async Task SameMessageAfterNewContext_SkipsHandler()
    {
        var message = await SeedAsync();
        var id = Guid.NewGuid();
        await using (var db = CreateDb()) Assert.True(await ProcessAsync(db, id, message));
        await using (var db = CreateDb())
        {
            Assert.False(await new InboxProcessor(db, TimeProvider.System).ProcessAsync(id,
                QueueNames.OrdersCourierAssigned, _ => throw new InvalidOperationException("Duplicate handler call"),
                CancellationToken.None));
        }
        await using var verification = CreateDb();
        Assert.Single(await verification.InboxMessages.ToListAsync());
        Assert.Equal(message.CourierId, (await verification.Orders.SingleAsync()).CourierId);
    }

    [PostgresFact]
    public async Task ConcurrentDifferentMessageIds_AssignSameOrderOnce()
    {
        var message = await SeedAsync();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = CreateDb();
            Assert.True(await ProcessAsync(db, Guid.NewGuid(), message));
        }));
        await using var verification = CreateDb();
        Assert.Equal(8, await verification.InboxMessages.CountAsync());
        var order = await verification.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Assigned, order.Status);
        Assert.Equal(message.CourierId, order.CourierId);
    }

    [PostgresFact]
    public async Task ConflictingCourier_RollsBackInboxAndPreservesAssignment()
    {
        var message = await SeedAsync();
        await using (var db = CreateDb()) Assert.True(await ProcessAsync(db, Guid.NewGuid(), message));
        await using (var db = CreateDb())
        {
            await Assert.ThrowsAsync<DomainException>(() => ProcessAsync(db, Guid.NewGuid(),
                message with { CourierId = Guid.NewGuid() }));
        }
        await using var verification = CreateDb();
        Assert.Single(await verification.InboxMessages.ToListAsync());
        Assert.Equal(message.CourierId, (await verification.Orders.SingleAsync()).CourierId);
    }

    [PostgresFact]
    public async Task SaveFails_RollsBackInboxAndOrder_ThenRetrySucceeds()
    {
        var message = await SeedAsync();
        var id = Guid.NewGuid();
        await using (var db = CreateDb(new FailSaveInterceptor()))
        {
            await Assert.ThrowsAsync<IOException>(() => ProcessAsync(db, id, message));
        }
        await using (var verification = CreateDb())
        {
            Assert.Empty(await verification.InboxMessages.ToListAsync());
            var order = await verification.Orders.SingleAsync();
            Assert.Equal(OrderStatus.Created, order.Status);
            Assert.Null(order.CourierId);
        }
        await using var retryDb = CreateDb();
        Assert.True(await ProcessAsync(retryDb, id, message));
    }

    [PostgresFact]
    public async Task LateAssignment_DoesNotResurrectCancelledOrder()
    {
        var message = await SeedAsync(order => order.Cancel());
        await using (var db = CreateDb()) Assert.True(await ProcessAsync(db, Guid.NewGuid(), message));
        await using var verification = CreateDb();
        var order = await verification.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Null(order.CourierId);
        Assert.Single(await verification.InboxMessages.ToListAsync());
    }

    [PostgresFact]
    public async Task RepeatedAssignment_DoesNotRegressDeliveredOrder()
    {
        var message = await SeedAsync();
        await using (var db = CreateDb())
        {
            await ProcessAsync(db, Guid.NewGuid(), message);
            var order = await db.Orders.SingleAsync();
            order.MarkPickedUp();
            order.MarkDelivered();
            await db.SaveChangesAsync();
        }
        await using (var db = CreateDb()) Assert.True(await ProcessAsync(db, Guid.NewGuid(), message));
        await using var verification = CreateDb();
        Assert.Equal(OrderStatus.Delivered, (await verification.Orders.SingleAsync()).Status);
        Assert.Equal(2, await verification.InboxMessages.CountAsync());
    }

    [PostgresFact]
    public async Task UnknownOrder_IsNotMarkedProcessed()
    {
        await using (var db = CreateDb())
        {
            var message = new CourierAssigned(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ProcessAsync(db, Guid.NewGuid(), message));
        }
        await using var verification = CreateDb();
        Assert.Empty(await verification.InboxMessages.ToListAsync());
    }

    private OrdersDbContext CreateDb(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention().AddInterceptors(interceptors).Options);

    private static Task<bool> ProcessAsync(OrdersDbContext db, Guid messageId, CourierAssigned message)
    {
        var handler = new CourierAssignedHandler(db);
        return new InboxProcessor(db, TimeProvider.System).ProcessAsync(messageId, QueueNames.OrdersCourierAssigned,
            token => handler.HandleAsync(message, token), CancellationToken.None);
    }

    private async Task<CourierAssigned> SeedAsync(Action<Order>? configure = null)
    {
        await using var db = CreateDb();
        var order = Order.Create(GeoPoint.Create(25, 55), GeoPoint.Create(26, 56), 12000, 8, DateTimeOffset.UtcNow);
        configure?.Invoke(order);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return new CourierAssigned(order.Id, Guid.NewGuid(), DateTimeOffset.UtcNow);
    }

    private sealed class FailSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new IOException("Save failed before inbox commit");
    }
}
