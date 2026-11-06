using Dispatch.Infrastructure.Inbox;
using Dispatch.Infrastructure.Outbox;
using Dispatch.Infrastructure.Sagas;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Persistence;

public sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    internal DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    internal DbSet<ReceivedOrder> ReceivedOrders => Set<ReceivedOrder>();
    public DbSet<AssignCourierState> AssignCourierSagas => Set<AssignCourierState>();
    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispatchDbContext).Assembly);
}
