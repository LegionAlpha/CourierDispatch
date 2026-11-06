using Dispatch.Application.Delivery.EstimateDelivery;
using Dispatch.Domain;
using Dispatch.Grpc;
using Grpc.Core;
using MediatR;
using DomainGeoPoint = Dispatch.Domain.Delivery.GeoPoint;

namespace Dispatch.Api.Services;

public sealed class DispatchGrpcService(ISender sender) : DispatchService.DispatchServiceBase
{
    public override async Task<EstimateResponse> EstimateDelivery(EstimateRequest request, ServerCallContext context)
    {
        if (request.From is null || request.To is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Both from and to coordinates are required."));
        }

        try
        {
            var query = new EstimateDeliveryQuery(
                DomainGeoPoint.Create(request.From.Lat, request.From.Lon),
                DomainGeoPoint.Create(request.To.Lat, request.To.Lon));

            var estimate = await sender.Send(query, context.CancellationToken);

            return new EstimateResponse
            {
                PriceMinor = estimate.PriceMinor,
                EtaMinutes = estimate.EtaMinutes,
                AvailableCouriersNearby = estimate.AvailableCouriersNearby
            };
        }
        catch (DomainException exception)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message));
        }
    }
}
