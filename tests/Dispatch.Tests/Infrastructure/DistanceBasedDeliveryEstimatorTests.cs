using Dispatch.Domain.Delivery;
using Dispatch.Infrastructure.Delivery;

namespace Dispatch.Tests.Infrastructure;

public class DistanceBasedDeliveryEstimatorTests
{
    private readonly DistanceBasedDeliveryEstimator _estimator = new();

    [Fact]
    public async Task EstimateAsync_SamePoint_ReturnsBasePriceAndPreparationTime()
    {
        var point = GeoPoint.Create(25.2048, 55.2708);

        var estimate = await _estimator.EstimateAsync(point, point, CancellationToken.None);

        Assert.Equal(10000, estimate.PriceMinor);
        Assert.Equal(5, estimate.EtaMinutes);
        Assert.Equal(0, estimate.AvailableCouriersNearby);
    }

    [Fact]
    public async Task EstimateAsync_OneDegreeAtEquator_UsesDistanceAndRoundsUp()
    {
        // One degree on the equator is approximately 111.195 km.
        var estimate = await _estimator.EstimateAsync(
            GeoPoint.Create(0, 0), GeoPoint.Create(0, 1), CancellationToken.None);

        Assert.Equal(343586, estimate.PriceMinor);
        Assert.Equal(339, estimate.EtaMinutes);
    }

    [Fact]
    public async Task EstimateAsync_ReversedRoute_ReturnsSameEstimate()
    {
        var from = GeoPoint.Create(51.5074, -0.1278);
        var to = GeoPoint.Create(40.7128, -74.0060);

        var forward = await _estimator.EstimateAsync(from, to, CancellationToken.None);
        var reverse = await _estimator.EstimateAsync(to, from, CancellationToken.None);

        Assert.Equal(forward, reverse);
        // London to New York is approximately 5,570 km on a great-circle route.
        Assert.InRange(forward.PriceMinor, 16700000, 16750000);
    }

    [Fact]
    public async Task EstimateAsync_AcrossDateLine_UsesShortDistance()
    {
        var estimate = await _estimator.EstimateAsync(
            GeoPoint.Create(0, 179.9), GeoPoint.Create(0, -179.9), CancellationToken.None);

        Assert.InRange(estimate.PriceMinor, 76000, 77000);
        Assert.Equal(72, estimate.EtaMinutes);
    }

    [Fact]
    public async Task EstimateAsync_AntipodalPoints_ReturnsFinitePositiveEstimate()
    {
        var estimate = await _estimator.EstimateAsync(
            GeoPoint.Create(45, 0), GeoPoint.Create(-45, 180), CancellationToken.None);

        Assert.InRange(estimate.PriceMinor, 60000000, 60100000);
        Assert.InRange(estimate.EtaMinutes, 60000, 60100);
    }

    [Fact]
    public async Task EstimateAsync_CancelledToken_Throws()
    {
        var point = GeoPoint.Create(0, 0);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _estimator.EstimateAsync(point, point, new CancellationToken(canceled: true)));
    }
}
