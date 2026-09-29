using Orders.Application.Orders.CreateOrder;

namespace Orders.Api.Contracts;

public sealed record CreateOrderRequest
{
    public required double FromLatitude { get; init; }
    public required double FromLongitude { get; init; }
    public required double ToLatitude { get; init; }
    public required double ToLongitude { get; init; }

    public CreateOrderCommand ToCommand() =>
        new(FromLatitude, FromLongitude, ToLatitude, ToLongitude);
}
