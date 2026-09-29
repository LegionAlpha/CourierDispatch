using Orders.Domain.Orders;

namespace Orders.Application.Abstractions;

public interface IDeliveryEstimator
{
    Task<DeliveryEstimate> EstimateAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken);
}
