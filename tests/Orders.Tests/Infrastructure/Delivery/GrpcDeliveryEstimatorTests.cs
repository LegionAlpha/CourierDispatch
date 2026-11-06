using Dispatch.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Orders.Application.Abstractions;
using Orders.Infrastructure.Delivery;
using DomainGeoPoint = Orders.Domain.Orders.GeoPoint;

namespace Orders.Tests.Infrastructure.Delivery;

public class GrpcDeliveryEstimatorTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
    private static readonly DomainGeoPoint _from = DomainGeoPoint.Create(25.2048, 55.2708);
    private static readonly DomainGeoPoint _to = DomainGeoPoint.Create(25.1972, 55.2744);

    [Fact]
    public async Task EstimateAsync_MapsRequestAndResponse_PassesDeadlineAndCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new StubDispatchClient((_, _) => Task.FromResult(new EstimateResponse
        {
            PriceMinor = 45678,
            EtaMinutes = 17,
            AvailableCouriersNearby = 4
        }));
        var estimator = CreateEstimator(client, timeoutSeconds: 7);

        var estimate = await estimator.EstimateAsync(_from, _to, cancellation.Token);

        Assert.Equal(new DeliveryEstimate(45678, 17), estimate);
        Assert.Equal(_from.Latitude, client.Request!.From.Lat);
        Assert.Equal(_from.Longitude, client.Request.From.Lon);
        Assert.Equal(_to.Latitude, client.Request.To.Lat);
        Assert.Equal(_to.Longitude, client.Request.To.Lon);
        Assert.Equal(_now.UtcDateTime.AddSeconds(7), client.Options.Deadline);
        Assert.Equal(cancellation.Token, client.Options.CancellationToken);
        Assert.True(client.CallDisposed);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable, DeliveryEstimationFailure.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded, DeliveryEstimationFailure.TimedOut)]
    [InlineData(StatusCode.Internal, DeliveryEstimationFailure.Failed)]
    [InlineData(StatusCode.Unknown, DeliveryEstimationFailure.Failed)]
    [InlineData(StatusCode.InvalidArgument, DeliveryEstimationFailure.Failed)]
    [InlineData(StatusCode.Cancelled, DeliveryEstimationFailure.Failed)]
    public async Task EstimateAsync_RpcFailure_ReturnsApplicationError(
        StatusCode status, DeliveryEstimationFailure expectedFailure)
    {
        var rpcException = new RpcException(new Status(status, "private-dispatch-detail"));
        var client = new StubDispatchClient((_, _) => Task.FromException<EstimateResponse>(rpcException));

        var exception = await Assert.ThrowsAsync<DeliveryEstimationException>(() =>
            CreateEstimator(client).EstimateAsync(_from, _to, CancellationToken.None));

        Assert.Equal(expectedFailure, exception.Failure);
        Assert.Same(rpcException, exception.InnerException);
        Assert.DoesNotContain("private-dispatch-detail", exception.Message);
        Assert.True(client.CallDisposed);
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(10000, 0)]
    [InlineData(10000, -1)]
    public async Task EstimateAsync_InvalidResponse_RejectsEstimate(long priceMinor, int etaMinutes)
    {
        var client = new StubDispatchClient((_, _) => Task.FromResult(new EstimateResponse
        {
            PriceMinor = priceMinor,
            EtaMinutes = etaMinutes
        }));

        var exception = await Assert.ThrowsAsync<DeliveryEstimationException>(() =>
            CreateEstimator(client).EstimateAsync(_from, _to, CancellationToken.None));

        Assert.Equal(DeliveryEstimationFailure.Failed, exception.Failure);
    }

    [Fact]
    public async Task EstimateAsync_AlreadyCancelled_DoesNotCallDispatch()
    {
        var client = new StubDispatchClient((_, _) => throw new InvalidOperationException("Must not be called."));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateEstimator(client).EstimateAsync(_from, _to, new CancellationToken(canceled: true)));

        Assert.Null(client.Request);
    }

    [Fact]
    public async Task EstimateAsync_CallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new StubDispatchClient((_, _) =>
        {
            cancellation.Cancel();
            return Task.FromException<EstimateResponse>(new RpcException(new Status(StatusCode.Cancelled, "Cancelled")));
        });

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateEstimator(client).EstimateAsync(_from, _to, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(client.CallDisposed);
    }

    private static GrpcDeliveryEstimator CreateEstimator(StubDispatchClient client, int timeoutSeconds = 5) =>
        new(client, Options.Create(new DispatchGrpcOptions { TimeoutSeconds = timeoutSeconds }), new FakeTimeProvider(_now));

    private sealed class StubDispatchClient(Func<EstimateRequest, CallOptions, Task<EstimateResponse>> respond)
        : DispatchService.DispatchServiceClient
    {
        public EstimateRequest? Request { get; private set; }
        public CallOptions Options { get; private set; }
        public bool CallDisposed { get; private set; }

        public override AsyncUnaryCall<EstimateResponse> EstimateDeliveryAsync(EstimateRequest request, CallOptions options)
        {
            Request = request;
            Options = options;

            return new AsyncUnaryCall<EstimateResponse>(
                respond(request, options),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => CallDisposed = true);
        }
    }
}
