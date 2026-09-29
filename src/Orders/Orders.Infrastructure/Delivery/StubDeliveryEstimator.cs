using Orders.Application.Abstractions;
using Orders.Domain.Orders;

namespace Orders.Infrastructure.Delivery;

internal sealed class StubDeliveryEstimator : IDeliveryEstimator
{
    public Task<DeliveryEstimate> EstimateAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken) =>
        Task.FromResult(new DeliveryEstimate(PriceMinor: 30000, EtaMinutes: 25));
}
