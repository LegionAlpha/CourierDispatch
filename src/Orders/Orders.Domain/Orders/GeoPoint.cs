namespace Orders.Domain.Orders;

public readonly record struct GeoPoint
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoPoint(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude < -90 || latitude > 90)
        {
            throw new DomainException("Latitude must be between -90 and 90 degrees.");
        }

        if (double.IsNaN(longitude) || longitude < -180 || longitude > 180)
        {
            throw new DomainException("Longitude must be between -180 and 180 degrees.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }
}
