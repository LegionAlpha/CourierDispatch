namespace Orders.Infrastructure.Delivery;

internal sealed class DispatchGrpcOptions
{
    public const string SectionName = "Dispatch";

    public string Address { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 5;
}
