namespace Orders.Infrastructure.Time;

internal sealed class MicrosecondPrecisionTimeProvider(TimeProvider inner) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        var now = inner.GetUtcNow();

        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
