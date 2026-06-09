using MarketData.Application.Monitoring;

namespace MarketData.UnitTests.Application;

/// <summary>Снапшот метрик аккумулирует счётчики для лог-репорта.</summary>
public class MarketDataMetricsTests
{
    [Fact]
    public void Snapshot_AccumulatesCounters()
    {
        using var metrics = new MarketDataMetrics();
        metrics.SetChannelDepthProvider(() => 7);

        metrics.RecordReceived("ExchangeA");
        metrics.RecordReceived("ExchangeB");
        metrics.RecordParseFailure("ExchangeA");
        metrics.RecordDuplicate();
        metrics.RecordBatchWritten(count: 50, latencyMs: 12.5);

        var s = metrics.Snapshot();

        Assert.Equal(2, s.Received);
        Assert.Equal(50, s.Written);
        Assert.Equal(1, s.Duplicates);
        Assert.Equal(1, s.ParseFailures);
        Assert.Equal(7, s.ChannelDepth);
    }
}
