using System.Text.Json;
using Dispatch.Infrastructure.Outbox;
using Dispatch.Infrastructure.Persistence;
using Dispatch.Infrastructure.Sagas;
using Dispatch.Tests.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Shared.Contracts.Dispatch;

namespace Dispatch.Tests.Outbox;

[Trait("Category", "PostgreSQL")]
public sealed class DispatchOutboxPostgresTests : IAsyncLifetime
{
    private readonly string _databaseName = $"dispatch_outbox_test_{Guid.NewGuid():N}";
    private readonly TestClock _clock = new();
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
        if (!_databaseCreated) return;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task FailedPublication_RemainsPending_AndRetriesWithTheSameId()
    {
        var id = await SeedAsync();
        var publisher = new Publisher { Fail = true };
        await using (var db = CreateDb()) Assert.True(await Processor(db, publisher).ProcessNextAsync(CancellationToken.None));
        await using (var db = CreateDb())
        {
            var pending = await db.OutboxMessages.SingleAsync();
            Assert.Null(pending.ProcessedAt);
            Assert.Equal(1, pending.Attempts);
            Assert.Equal(_clock.Now.AddSeconds(5), pending.NextAttemptAt);
            Assert.Contains("Broker offline", pending.LastError);
            Assert.False(await Processor(db, publisher).ProcessNextAsync(CancellationToken.None));
        }
        publisher.Fail = false;
        _clock.Now = _clock.Now.AddSeconds(5);
        await using (var db = CreateDb()) Assert.True(await Processor(db, publisher).ProcessNextAsync(CancellationToken.None));
        Assert.Equal(new[] { id, id }, publisher.Ids);
        await using var verification = CreateDb();
        var sent = await verification.OutboxMessages.SingleAsync();
        Assert.NotNull(sent.ProcessedAt);
        Assert.Null(sent.LastError);
        Assert.Equal(2, sent.Attempts);
    }

    [PostgresFact]
    public async Task SaveAfterPublicationFails_RetryPreservesMessageId()
    {
        var id = await SeedAsync();
        var publisher = new Publisher();
        await using (var db = CreateDb(new FailSave()))
        {
            await Assert.ThrowsAsync<IOException>(() => Processor(db, publisher).ProcessNextAsync(CancellationToken.None));
        }
        await using (var db = CreateDb())
        {
            Assert.Null((await db.OutboxMessages.SingleAsync()).ProcessedAt);
            Assert.True(await Processor(db, publisher).ProcessNextAsync(CancellationToken.None));
        }
        Assert.Equal(new[] { id, id }, publisher.Ids);
    }

    [PostgresFact]
    public async Task Upgrade_StagesEventsForPreviouslyAssignedSagasOnce()
    {
        await using var db = CreateDb();
        var previous = db.Database.GetMigrations().Single(name => name.EndsWith("_AddAssignCourierSaga"));
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var state = new AssignCourierState
        {
            CorrelationId = Guid.NewGuid(),
            CurrentState = "Assigned",
            InitialMessageId = Guid.NewGuid(),
            CourierId = Guid.NewGuid(),
            OrderCreatedAt = _clock.Now,
            StartedAt = _clock.Now,
            AssignedAt = _clock.Now
        };
        db.AssignCourierSagas.Add(state);
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();

        var row = await db.OutboxMessages.SingleAsync();
        var message = JsonSerializer.Deserialize<CourierAssigned>(row.Payload, OutboxWriter.SerializerOptions)!;
        Assert.Equal(state.CorrelationId, message.OrderId);
        Assert.Equal(state.CourierId, message.CourierId);
        Assert.Equal(state.AssignedAt, message.AssignedAt);
        Assert.Null(row.ProcessedAt);
    }

    private DispatchDbContext CreateDb(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<DispatchDbContext>().UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention().AddInterceptors(interceptors).Options);

    private OutboxProcessor Processor(DispatchDbContext db, IOutboxMessagePublisher publisher) => new(
        db, publisher, _clock, Options.Create(new OutboxOptions()), NullLogger<OutboxProcessor>.Instance);

    private async Task<Guid> SeedAsync()
    {
        await using var db = CreateDb();
        await using var transaction = await db.Database.BeginTransactionAsync();
        new OutboxWriter(db).Add(new CourierAssigned(Guid.NewGuid(), Guid.NewGuid(), _clock.Now));
        var id = db.OutboxMessages.Local.Single().Id;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return id;
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Publisher : IOutboxMessagePublisher
    {
        public bool Fail { get; set; }
        public List<Guid> Ids { get; } = [];
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            Ids.Add(message.Id);
            return Fail ? Task.FromException(new IOException("Broker offline")) : Task.CompletedTask;
        }
    }

    private sealed class FailSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new IOException("Failed after publication");
    }
}
