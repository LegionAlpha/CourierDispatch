using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;
using Shared.Contracts.Dispatch;

namespace Orders.Infrastructure.Messaging;

internal sealed class CourierAssignedHandler(OrdersDbContext dbContext) : ICourierAssignedHandler
{
    public async Task HandleAsync(CourierAssigned message, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("CourierAssigned requires the inbox transaction.");
        }
        // Different MessageIds for the same order must also be serialized. Lock first,
        // then let EF map the order's complex properties using its regular LINQ query.
        await dbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM orders WHERE id = {message.OrderId} FOR UPDATE
            """).ToListAsync(cancellationToken);
        var order = await dbContext.Orders.SingleOrDefaultAsync(order => order.Id == message.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order '{message.OrderId}' does not exist.");

        order.ApplyCourierAssignment(message.CourierId);
    }
}
