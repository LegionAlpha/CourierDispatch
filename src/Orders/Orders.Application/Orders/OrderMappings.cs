using Orders.Domain.Orders;

namespace Orders.Application.Orders;

internal static class OrderMappings
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.Status,
        order.From.Latitude,
        order.From.Longitude,
        order.To.Latitude,
        order.To.Longitude,
        order.PriceMinor,
        order.EtaMinutes,
        order.CourierId,
        order.CreatedAt);
}
