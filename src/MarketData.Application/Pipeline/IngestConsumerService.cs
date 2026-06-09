using System.Diagnostics;
using System.Threading.Channels;
using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;
using MarketData.Application.Monitoring;
using MarketData.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Application.Pipeline;

/// <summary>
/// Единственный консьюмер канала: читает тики, дедуплицирует, копит батч
/// до <see cref="PipelineOptions.BatchSize"/> ИЛИ <see cref="PipelineOptions.BatchTimeoutMs"/> (что раньше),
/// и пишет батч в <see cref="ITickSink"/>. Один writer на БД — меньше round-trip, упорядоченность.
/// </summary>
public sealed class IngestConsumerService(
    IngestPipeline pipeline,
    IDeduplicator deduplicator,
    ITickSink sink,
    MarketDataMetrics metrics,
    IOptions<PipelineOptions> options,
    ILogger<IngestConsumerService> logger) : BackgroundService
{
    private readonly PipelineOptions _options = options.Value;

    private long _written;
    private long _duplicates;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = pipeline.Reader;
        var batchTimeout = TimeSpan.FromMilliseconds(_options.BatchTimeoutMs);
        var batch = new List<Tick>(_options.BatchSize);

        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                using var window = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                window.CancelAfter(batchTimeout);

                await FillBatchAsync(reader, batch, stoppingToken, window.Token);
                await FlushAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            DrainRemaining(reader, batch);
            await FlushAsync(batch, CancellationToken.None);
        }

        logger.LogInformation(
            "Ingest consumer stopped, written={Written}, duplicates={Duplicates}", _written, _duplicates);
    }

    /// <summary>Наполняет батч до размера или до истечения окна <paramref name="windowToken"/>.</summary>
    private async Task FillBatchAsync(
        ChannelReader<Tick> reader,
        List<Tick> batch,
        CancellationToken stoppingToken,
        CancellationToken windowToken)
    {
        while (batch.Count < _options.BatchSize)
        {
            if (reader.TryRead(out var tick))
            {
                Accept(tick, batch);
                continue;
            }

            try
            {
                if (!await reader.WaitToReadAsync(windowToken))
                    return; // канал закрыт
            }
            catch (OperationCanceledException)
                when (windowToken.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            {
                return; // истёк таймаут батча
            }
        }
    }

    private void DrainRemaining(ChannelReader<Tick> reader, List<Tick> batch)
    {
        while (reader.TryRead(out var tick))
            Accept(tick, batch);
    }

    private void Accept(in Tick tick, List<Tick> batch)
    {
        if (deduplicator.IsNew(tick))
        {
            batch.Add(tick);
        }
        else
        {
            _duplicates++;
            metrics.RecordDuplicate();
        }
    }

    private async Task FlushAsync(List<Tick> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
            return;

        try
        {
            var start = Stopwatch.GetTimestamp();
            await sink.WriteBatchAsync(batch, ct);
            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            _written += batch.Count;
            metrics.RecordBatchWritten(batch.Count, elapsedMs);
        }
        catch (Exception ex) when (ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to flush {Count} ticks on shutdown", batch.Count);
        }
        finally
        {
            batch.Clear();
        }
    }
}
