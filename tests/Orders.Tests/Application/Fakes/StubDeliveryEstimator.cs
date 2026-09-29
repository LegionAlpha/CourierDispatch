using Orders.Application.Abstractions;
using Orders.Domain.Orders;

namespace Orders.Tests.Application.Fakes;

internal sealed class StubDeliveryEstimator(DeliveryEstimate estimate) : IDeliveryEstimator
{
    public Task<DeliveryEstimate> EstimateAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken) =>
        Task.FromResult(estimate);
}
