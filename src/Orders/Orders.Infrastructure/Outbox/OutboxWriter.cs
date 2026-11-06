using System.Text.Json;
using Orders.Application.Abstractions;
using Orders.Infrastructure.Persistence;
using Shared.Contracts.Orders;

namespace Orders.Infrastructure.Outbox;

internal sealed class OutboxWriter(OrdersDbContext dbContext) : IOutboxWriter
{
    internal const string OrderCreatedType = "orders.order-created.v1";
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Add(OrderCreated message) => dbContext.OutboxMessages.Add(new OutboxMessage
    {
        Id = Guid.CreateVersion7(),
        Type = OrderCreatedType,
        Payload = JsonSerializer.Serialize(message, SerializerOptions),
        OccurredAt = message.CreatedAt,
        NextAttemptAt = message.CreatedAt
    });
}
