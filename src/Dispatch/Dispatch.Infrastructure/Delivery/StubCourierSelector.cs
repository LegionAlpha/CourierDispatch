using Dispatch.Application.Abstractions;
using Dispatch.Domain.Delivery;

namespace Dispatch.Infrastructure.Delivery;

internal sealed class StubCourierSelector : ICourierSelector
{
    // Step 2d: a demo courier, with no availability lookup or reservation yet.
    internal static readonly Guid CourierId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Task<Guid> SelectAsync(Guid orderId, GeoPoint pickup, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CourierId);
    }
}
