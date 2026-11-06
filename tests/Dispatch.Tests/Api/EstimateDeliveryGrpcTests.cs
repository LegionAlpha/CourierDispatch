using Dispatch.Grpc;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;

namespace Dispatch.Tests.Api;

public sealed class EstimateDeliveryGrpcTests : IClassFixture<DispatchApiFactory>, IDisposable
{
    private static readonly Method<EstimateRequest, EstimateResponse> _estimateMethod = new(
        MethodType.Unary,
        "dispatch.v1.DispatchService",
        "EstimateDelivery",
        Marshallers.Create(request => request.ToByteArray(), bytes => EstimateRequest.Parser.ParseFrom(bytes)),
        Marshallers.Create(response => response.ToByteArray(), bytes => EstimateResponse.Parser.ParseFrom(bytes)));

    private readonly HttpClient _httpClient;
    private readonly GrpcChannel _channel;

    public EstimateDeliveryGrpcTests(DispatchApiFactory factory)
    {
        _httpClient = factory.CreateClient();
        _channel = GrpcChannel.ForAddress(_httpClient.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = _httpClient
        });
    }

    [Fact]
    public async Task EstimateDelivery_ValidRequest_ReturnsDistanceBasedEstimate()
    {
        var response = await EstimateAsync(new EstimateRequest
        {
            From = new GeoPoint { Lat = 0, Lon = 0 },
            To = new GeoPoint { Lat = 0, Lon = 1 }
        });

        Assert.Equal(343586, response.PriceMinor);
        Assert.Equal(339, response.EtaMinutes);
        Assert.Equal(0, response.AvailableCouriersNearby);
    }

    [Fact]
    public async Task EstimateDelivery_ZeroCoordinates_AreValid()
    {
        var response = await EstimateAsync(new EstimateRequest
        {
            From = new GeoPoint(),
            To = new GeoPoint()
        });

        Assert.Equal(10000, response.PriceMinor);
        Assert.Equal(5, response.EtaMinutes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task EstimateDelivery_MissingPoint_ReturnsInvalidArgument(bool hasFrom, bool hasTo)
    {
        var request = new EstimateRequest();
        if (hasFrom)
        {
            request.From = new GeoPoint();
        }

        if (hasTo)
        {
            request.To = new GeoPoint();
        }

        var exception = await Assert.ThrowsAsync<RpcException>(() => EstimateAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Theory]
    [InlineData(91, 0, true)]
    [InlineData(-91, 0, false)]
    [InlineData(0, 181, true)]
    [InlineData(0, -181, false)]
    [InlineData(double.NaN, 0, true)]
    [InlineData(0, double.NaN, false)]
    [InlineData(double.PositiveInfinity, 0, false)]
    [InlineData(0, double.NegativeInfinity, true)]
    public async Task EstimateDelivery_InvalidCoordinates_ReturnsInvalidArgument(
        double latitude, double longitude, bool invalidFrom)
    {
        var invalid = new GeoPoint { Lat = latitude, Lon = longitude };
        var request = new EstimateRequest
        {
            From = invalidFrom ? invalid : new GeoPoint(),
            To = invalidFrom ? new GeoPoint() : invalid
        };

        var exception = await Assert.ThrowsAsync<RpcException>(() => EstimateAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _httpClient.Dispose();
    }

    private async Task<EstimateResponse> EstimateAsync(EstimateRequest request)
    {
        using var call = _channel.CreateCallInvoker().AsyncUnaryCall(
            _estimateMethod, null, new CallOptions(deadline: DateTime.UtcNow.AddSeconds(10)), request);

        return await call.ResponseAsync;
    }
}
