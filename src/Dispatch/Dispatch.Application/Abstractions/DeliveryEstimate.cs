namespace Dispatch.Application.Abstractions;

public sealed record DeliveryEstimate(long PriceMinor, int EtaMinutes, int AvailableCouriersNearby);
