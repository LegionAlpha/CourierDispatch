namespace Dispatch.Infrastructure.Persistence;

// A local receipt of OrderCreated, not the Orders aggregate or courier-assignment state.
internal sealed class ReceivedOrder
{
    public Guid OrderId { get; set; }
    public double FromLatitude { get; set; }
    public double FromLongitude { get; set; }
    public double ToLatitude { get; set; }
    public double ToLongitude { get; set; }
    public long PriceMinor { get; set; }
    public int EtaMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
