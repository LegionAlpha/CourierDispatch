namespace Shared.Contracts.Orders;

public sealed record OrderCreated(
    Guid OrderId,
    double FromLatitude,
    double FromLongitude,
    double ToLatitude,
    double ToLongitude,
    long PriceMinor,
    int EtaMinutes,
    DateTimeOffset CreatedAt);
