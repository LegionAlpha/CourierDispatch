using Dispatch.Domain.Delivery;

namespace Dispatch.Application.Abstractions;

public interface ICourierSelector
{
    Task<Guid> SelectAsync(Guid orderId, GeoPoint pickup, CancellationToken cancellationToken);
}
