using System.Text.Json;
using Dispatch.Application.Abstractions;
using Dispatch.Infrastructure.Persistence;
using Shared.Contracts.Dispatch;

namespace Dispatch.Infrastructure.Outbox;

internal sealed class OutboxWriter(DispatchDbContext dbContext) : IOutboxWriter
{
    internal const string CourierAssignedType = "dispatch.courier-assigned.v1";
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Add(CourierAssigned message)
    {
        // The saga repository owns this transaction and saves both state and the staged event.
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("CourierAssigned must be staged in the saga transaction.");
        }

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = CourierAssignedType,
            Payload = JsonSerializer.Serialize(message, SerializerOptions),
            OccurredAt = message.AssignedAt,
            NextAttemptAt = message.AssignedAt
        });
    }
}
