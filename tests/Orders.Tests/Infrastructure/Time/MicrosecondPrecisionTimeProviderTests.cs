using Microsoft.Extensions.Time.Testing;
using Orders.Infrastructure.Time;

namespace Orders.Tests.Infrastructure.Time;

public class MicrosecondPrecisionTimeProviderTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetUtcNow_DropsSubMicrosecondTicks()
    {
        var provider = new MicrosecondPrecisionTimeProvider(new FakeTimeProvider(_now.AddTicks(1_234_567)));

        var result = provider.GetUtcNow();

        Assert.Equal(_now.AddTicks(1_234_560), result);
    }

    [Fact]
    public void GetUtcNow_KeepsTimeAlreadyInMicroseconds()
    {
        var provider = new MicrosecondPrecisionTimeProvider(new FakeTimeProvider(_now.AddTicks(1_234_560)));

        var result = provider.GetUtcNow();

        Assert.Equal(_now.AddTicks(1_234_560), result);
    }
}
