using MarketData.Application.Configuration;
using MarketData.Infrastructure.Exchange;

namespace MarketData.UnitTests.Infrastructure;

/// <summary>Backoff реконнекта: экспоненциальный рост, full jitter в [delay/2, delay], потолок MaxDelay.</summary>
public class ReconnectBackoffTests
{
    private static readonly ReconnectOptions Options = new() { BaseDelayMs = 500, MaxDelayMs = 30_000 };

    [Theory]
    [InlineData(0, 500)]    // 500 * 2^0
    [InlineData(1, 1_000)]  // 500 * 2^1
    [InlineData(2, 2_000)]  // 500 * 2^2
    [InlineData(3, 4_000)]  // 500 * 2^3
    public void NextDelay_StaysWithinFullJitterBounds(int attempt, int ceiling)
    {
        // jitter ∈ [ceiling/2, ceiling): нижняя граница достижима (NextDouble=0), верхняя — нет.
        Assert.Equal(ceiling / 2, ReconnectBackoff.NextDelayMs(attempt, Options, new StubRandom(0.0)));
        Assert.InRange(
            ReconnectBackoff.NextDelayMs(attempt, Options, new StubRandom(0.999999)),
            ceiling / 2, ceiling);
    }

    [Fact]
    public void NextDelay_CapsAtMaxDelay()
    {
        // attempt 20 → 500 * 2^20 далеко за потолком, обрезается до MaxDelay.
        var atCeiling = ReconnectBackoff.NextDelayMs(20, Options, new StubRandom(0.999999));
        Assert.InRange(atCeiling, Options.MaxDelayMs / 2, Options.MaxDelayMs);

        var atFloor = ReconnectBackoff.NextDelayMs(20, Options, new StubRandom(0.0));
        Assert.Equal(Options.MaxDelayMs / 2, atFloor);
    }

    [Fact]
    public void NextDelay_NeverBelowOne()
    {
        var options = new ReconnectOptions { BaseDelayMs = 1, MaxDelayMs = 1 };
        Assert.True(ReconnectBackoff.NextDelayMs(0, options, new StubRandom(0.0)) >= 1);
    }

    /// <summary>Детерминированный <see cref="Random"/>: NextDouble всегда возвращает заданное значение.</summary>
    private sealed class StubRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }
}
