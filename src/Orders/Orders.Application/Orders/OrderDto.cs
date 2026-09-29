using Orders.Domain.Orders;

namespace Orders.Application.Orders;

public sealed record OrderDto(
    Guid Id,
    OrderStatus Status,
    double FromLatitude,
    double FromLongitude,
    double ToLatitude,
    double ToLongitude,
    long PriceMinor,
    int EtaMinutes,
    Guid? CourierId,
    DateTimeOffset CreatedAt);
