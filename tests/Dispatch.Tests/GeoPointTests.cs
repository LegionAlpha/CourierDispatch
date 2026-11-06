using Dispatch.Domain;
using Dispatch.Domain.Delivery;

namespace Dispatch.Tests;

public class GeoPointTests
{
    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(0, double.NegativeInfinity)]
    public void Create_InvalidCoordinates_Throws(double latitude, double longitude)
    {
        Assert.Throws<DomainException>(() => GeoPoint.Create(latitude, longitude));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public void Create_ValidCoordinates_PreservesValues(double latitude, double longitude)
    {
        var point = GeoPoint.Create(latitude, longitude);

        Assert.Equal(latitude, point.Latitude);
        Assert.Equal(longitude, point.Longitude);
    }
}
