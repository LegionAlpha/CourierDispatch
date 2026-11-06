using Dispatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Orders;

namespace Dispatch.Infrastructure.Messaging;

internal sealed class OrderCreatedHandler(DispatchDbContext dbContext, TimeProvider timeProvider) : IOrderCreatedHandler
{
    public async Task HandleAsync(OrderCreated message, CancellationToken cancellationToken)
    {
        var receivedAt = timeProvider.GetUtcNow();
        // Also protect the business key: the same order with another MessageId is not a new order.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO received_orders
                (order_id, from_latitude, from_longitude, to_latitude, to_longitude,
                 price_minor, eta_minutes, created_at, received_at)
            VALUES ({message.OrderId}, {message.FromLatitude}, {message.FromLongitude},
                {message.ToLatitude}, {message.ToLongitude}, {message.PriceMinor},
                {message.EtaMinutes}, {message.CreatedAt}, {receivedAt})
            ON CONFLICT (order_id) DO NOTHING
            """, cancellationToken);
    }
}
