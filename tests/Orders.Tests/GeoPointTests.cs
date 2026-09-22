using Orders.Domain;
using Orders.Domain.Orders;

namespace Orders.Tests;

public class GeoPointTests
{
    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(double.NaN, double.NaN)]
    [InlineData(0, double.NaN)]
    [InlineData(double.NaN, 0)]
    public void GeoPoint_InvalidCoordinates_Throws(double lat, double lon)
    {
        Assert.Throws<DomainException>(() => new GeoPoint(lat, lon));
    }

    [Theory]
    [InlineData(15, 46)]
    [InlineData(-24.214, 39.935)]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    public void GeoPoint_ValidCoordinates_StoresValues(double lat, double lon)
    {
        var geo = new GeoPoint(lat, lon);

        Assert.Equal(lat, geo.Latitude);
        Assert.Equal(lon, geo.Longitude);
    }
}
