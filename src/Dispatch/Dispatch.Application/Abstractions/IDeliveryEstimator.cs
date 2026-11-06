using Dispatch.Domain.Delivery;

namespace Dispatch.Application.Abstractions;

public interface IDeliveryEstimator
{
    Task<DeliveryEstimate> EstimateAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken);
}
