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
    public void Create_InvalidCoordinates_Throws(double lat, double lon)
    {
        Assert.Throws<DomainException>(() => GeoPoint.Create(lat, lon));
    }

    [Theory]
    [InlineData(15, 46)]
    [InlineData(-24.214, 39.935)]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    public void Create_ValidCoordinates_StoresValues(double lat, double lon)
    {
        var geo = GeoPoint.Create(lat, lon);

        Assert.Equal(lat, geo.Latitude);
        Assert.Equal(lon, geo.Longitude);
    }

    [Fact]
    public void SameCoordinates_AreEqual()
    {
        var a = GeoPoint.Create(55.75, 37.62);
        var b = GeoPoint.Create(55.75, 37.62);

        Assert.Equal(a, b);
    }
}
