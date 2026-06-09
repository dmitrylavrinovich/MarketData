using System.Diagnostics.Metrics;

namespace MarketData.Application.Monitoring;

/// <summary>
/// Метрики пайплайна на <see cref="Meter"/> (OTel/Prometheus-совместимо) + потокобезопасный снапшот
/// для периодического лог-репорта. Имя метра <see cref="MeterName"/> — точка подключения экспортёра.
/// </summary>
public sealed class MarketDataMetrics : IDisposable
{
    public const string MeterName = "MarketData";

    private readonly Meter _meter;
    private readonly Counter<long> _received;
    private readonly Counter<long> _written;
    private readonly Counter<long> _duplicates;
    private readonly Counter<long> _parseFailures;
    private readonly Histogram<int> _batchSize;
    private readonly Histogram<double> _writeLatencyMs;

    private long _receivedTotal;
    private long _writtenTotal;
    private long _duplicateTotal;
    private long _parseFailureTotal;

    private Func<int> _channelDepthProvider = static () => 0;

    public MarketDataMetrics()
    {
        _meter = new Meter(MeterName);
        _received = _meter.CreateCounter<long>("ticks_received_total", "ticks", "Тики, принятые от источников");
        _written = _meter.CreateCounter<long>("ticks_written_total", "ticks", "Тики, записанные в БД");
        _duplicates = _meter.CreateCounter<long>("ticks_duplicate_total", "ticks", "Отброшенные дубликаты");
        _parseFailures = _meter.CreateCounter<long>("ticks_parse_failures_total", "messages", "Сообщения, не прошедшие парсинг/нормализацию");
        _batchSize = _meter.CreateHistogram<int>("batch_size", "ticks", "Размер флашнутого батча");
        _writeLatencyMs = _meter.CreateHistogram<double>("db_write_latency_ms", "ms", "Латентность записи батча в БД");
        _meter.CreateObservableGauge("channel_depth", () => _channelDepthProvider(), "ticks", "Текущая глубина канала");
    }

    public void RecordReceived(string exchange)
    {
        _received.Add(1, new KeyValuePair<string, object?>("exchange", exchange));
        Interlocked.Increment(ref _receivedTotal);
    }

    public void RecordParseFailure(string exchange)
    {
        _parseFailures.Add(1, new KeyValuePair<string, object?>("exchange", exchange));
        Interlocked.Increment(ref _parseFailureTotal);
    }

    public void RecordDuplicate()
    {
        _duplicates.Add(1);
        Interlocked.Increment(ref _duplicateTotal);
    }

    public void RecordBatchWritten(int count, double latencyMs)
    {
        _written.Add(count);
        _batchSize.Record(count);
        _writeLatencyMs.Record(latencyMs);
        Interlocked.Add(ref _writtenTotal, count);
    }

    /// <summary>Источник для observable-гейджа <c>channel_depth</c> (задаётся при сборке DI).</summary>
    public void SetChannelDepthProvider(Func<int> provider) => _channelDepthProvider = provider;

    public MetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref _receivedTotal),
        Interlocked.Read(ref _writtenTotal),
        Interlocked.Read(ref _duplicateTotal),
        Interlocked.Read(ref _parseFailureTotal),
        _channelDepthProvider());

    public void Dispose() => _meter.Dispose();
}

/// <summary>Снимок накопленных счётчиков для лог-репорта.</summary>
public readonly record struct MetricsSnapshot(
    long Received, long Written, long Duplicates, long ParseFailures, int ChannelDepth);
