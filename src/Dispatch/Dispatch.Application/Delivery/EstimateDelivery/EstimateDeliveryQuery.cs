using Dispatch.Application.Abstractions;
using Dispatch.Domain.Delivery;
using MediatR;

namespace Dispatch.Application.Delivery.EstimateDelivery;

public sealed record EstimateDeliveryQuery(GeoPoint From, GeoPoint To) : IRequest<DeliveryEstimate>;
