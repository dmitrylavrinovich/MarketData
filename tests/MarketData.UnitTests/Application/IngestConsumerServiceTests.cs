using System.Collections.Concurrent;
using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;
using MarketData.Application.Pipeline;
using MarketData.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarketData.UnitTests.Application;

/// <summary>
/// Консьюмер: flush батча по достижении размера и по таймауту, применение дедупа,
/// дозапись остатка при остановке.
/// </summary>
public class IngestConsumerServiceTests
{
    private static readonly DateTimeOffset Ts = new(2026, 6, 8, 12, 0, 0, TimeSpan.Zero);

    private static Tick Tick(int i)
        => new("ExchangeA", "BTC-USDT", 100m + i, 1m, Ts.AddMilliseconds(i), Ts);

    private static IngestConsumerService Create(
        IngestPipeline pipeline, ITickSink sink, IDeduplicator dedup, PipelineOptions opts)
        => new(pipeline, dedup, sink, Options.Create(opts), NullLogger<IngestConsumerService>.Instance);

    [Fact]
    public async Task FlushesBatch_WhenBatchSizeReached()
    {
        var pipeline = new IngestPipeline(Options.Create(new PipelineOptions()));
        var sink = new RecordingSink();
        var opts = new PipelineOptions { BatchSize = 3, BatchTimeoutMs = 60_000 };
        var consumer = Create(pipeline, sink, new PassthroughDeduplicator(), opts);

        await consumer.StartAsync(CancellationToken.None);
        for (var i = 0; i < 3; i++)
            await pipeline.Writer.WriteAsync(Tick(i));

        var batch = await sink.WaitForBatchAsync();
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(3, batch.Count);
    }

    [Fact]
    public async Task FlushesBatch_WhenTimeoutElapses_BeforeBatchFull()
    {
        var pipeline = new IngestPipeline(Options.Create(new PipelineOptions()));
        var sink = new RecordingSink();
        var opts = new PipelineOptions { BatchSize = 1000, BatchTimeoutMs = 100 };
        var consumer = Create(pipeline, sink, new PassthroughDeduplicator(), opts);

        await consumer.StartAsync(CancellationToken.None);
        await pipeline.Writer.WriteAsync(Tick(0));
        await pipeline.Writer.WriteAsync(Tick(1));

        // Батч не заполнен (2 << 1000), но таймаут должен его вытолкнуть.
        var batch = await sink.WaitForBatchAsync();
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(2, batch.Count);
    }

    [Fact]
    public async Task SkipsDuplicates_BeforeWriting()
    {
        var pipeline = new IngestPipeline(Options.Create(new PipelineOptions()));
        var sink = new RecordingSink();
        var opts = new PipelineOptions { BatchSize = 2, BatchTimeoutMs = 60_000 };
        var consumer = Create(pipeline, sink, new OddRejectingDeduplicator(), opts);

        await consumer.StartAsync(CancellationToken.None);
        await pipeline.Writer.WriteAsync(Tick(0)); // принят
        await pipeline.Writer.WriteAsync(Tick(1)); // отклонён дедупом
        await pipeline.Writer.WriteAsync(Tick(2)); // принят → батч=2 → flush

        var batch = await sink.WaitForBatchAsync();
        await consumer.StopAsync(CancellationToken.None);

        Assert.Equal(2, batch.Count);
        Assert.All(batch, t => Assert.NotEqual(101m, t.Price)); // Tick(1) отброшен
    }

    private sealed class RecordingSink : ITickSink
    {
        private readonly ConcurrentQueue<IReadOnlyList<Tick>> _batches = new();
        private readonly SemaphoreSlim _signal = new(0);

        public Task WriteBatchAsync(IReadOnlyList<Tick> batch, CancellationToken ct)
        {
            _batches.Enqueue(batch.ToList());
            _signal.Release();
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<Tick>> WaitForBatchAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _signal.WaitAsync(cts.Token);
            _batches.TryDequeue(out var batch);
            return batch!;
        }
    }

    private sealed class PassthroughDeduplicator : IDeduplicator
    {
        public bool IsNew(in Tick tick) => true;
    }

    private sealed class OddRejectingDeduplicator : IDeduplicator
    {
        public bool IsNew(in Tick tick) => tick.Price != 101m;
    }
}
