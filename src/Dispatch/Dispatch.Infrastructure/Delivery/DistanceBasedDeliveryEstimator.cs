using Dispatch.Application.Abstractions;
using Dispatch.Domain.Delivery;

namespace Dispatch.Infrastructure.Delivery;

internal sealed class DistanceBasedDeliveryEstimator : IDeliveryEstimator
{
    private const double EarthRadiusKm = 6371.0088;
    private const long BasePriceMinor = 10000;
    private const long PricePerKmMinor = 3000;
    private const int PreparationMinutes = 5;
    private const double SpeedKmPerHour = 20;

    public Task<DeliveryEstimate> EstimateAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var distanceKm = CalculateDistanceKm(from, to);
        var priceMinor = BasePriceMinor + (long)Math.Ceiling(distanceKm * PricePerKmMinor);
        var etaMinutes = PreparationMinutes + (int)Math.Ceiling(distanceKm / SpeedKmPerHour * 60);

        return Task.FromResult(new DeliveryEstimate(priceMinor, etaMinutes, AvailableCouriersNearby: 0));
    }

    private static double CalculateDistanceKm(GeoPoint from, GeoPoint to)
    {
        var latitudeDelta = ToRadians(to.Latitude - from.Latitude);
        var longitudeDelta = ToRadians(to.Longitude - from.Longitude);
        var sinLatitude = Math.Sin(latitudeDelta / 2);
        var sinLongitude = Math.Sin(longitudeDelta / 2);

        // Haversine distance on a sphere; this stub does not account for roads or traffic.
        var haversine = sinLatitude * sinLatitude
            + Math.Cos(ToRadians(from.Latitude)) * Math.Cos(ToRadians(to.Latitude))
            * sinLongitude * sinLongitude;

        // Rounding near antipodal points can otherwise put the square root outside [0, 1].
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(Math.Clamp(haversine, 0, 1)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
