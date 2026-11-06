using Dispatch.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Orders.Application.Abstractions;
using DomainGeoPoint = Orders.Domain.Orders.GeoPoint;

namespace Orders.Infrastructure.Delivery;

internal sealed class GrpcDeliveryEstimator(
    DispatchService.DispatchServiceClient client,
    IOptions<DispatchGrpcOptions> options,
    TimeProvider timeProvider) : IDeliveryEstimator
{
    public async Task<DeliveryEstimate> EstimateAsync(
        DomainGeoPoint from, DomainGeoPoint to, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new EstimateRequest
        {
            From = new GeoPoint { Lat = from.Latitude, Lon = from.Longitude },
            To = new GeoPoint { Lat = to.Latitude, Lon = to.Longitude }
        };

        try
        {
            using var call = client.EstimateDeliveryAsync(
                request,
                deadline: timeProvider.GetUtcNow().UtcDateTime.AddSeconds(options.Value.TimeoutSeconds),
                cancellationToken: cancellationToken);

            var response = await call.ResponseAsync;
            if (response.PriceMinor < 0 || response.EtaMinutes <= 0)
            {
                throw new DeliveryEstimationException(DeliveryEstimationFailure.Failed);
            }

            return new DeliveryEstimate(response.PriceMinor, response.EtaMinutes);
        }
        catch (RpcException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Delivery estimation was cancelled.", exception, cancellationToken);
        }
        catch (RpcException exception)
        {
            var failure = exception.StatusCode switch
            {
                StatusCode.Unavailable => DeliveryEstimationFailure.Unavailable,
                StatusCode.DeadlineExceeded => DeliveryEstimationFailure.TimedOut,
                _ => DeliveryEstimationFailure.Failed
            };

            throw new DeliveryEstimationException(failure, exception);
        }
    }
}
