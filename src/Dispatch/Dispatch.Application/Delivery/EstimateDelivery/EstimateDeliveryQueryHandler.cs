using Dispatch.Application.Abstractions;
using MediatR;

namespace Dispatch.Application.Delivery.EstimateDelivery;

internal sealed class EstimateDeliveryQueryHandler(IDeliveryEstimator estimator)
    : IRequestHandler<EstimateDeliveryQuery, DeliveryEstimate>
{
    public Task<DeliveryEstimate> Handle(EstimateDeliveryQuery request, CancellationToken cancellationToken) =>
        estimator.EstimateAsync(request.From, request.To, cancellationToken);
}
