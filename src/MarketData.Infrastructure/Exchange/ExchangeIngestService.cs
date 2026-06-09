using MarketData.Application.Abstractions;
using MarketData.Application.Monitoring;
using MarketData.Application.Pipeline;
using MarketData.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Exchange;

/// <summary>
/// Producer одного источника: читает сырой поток клиента, парсит выбранным по <c>ExchangeOptions.Parser</c> парсером,
/// нормализует и пишет в канал пайплайна. По одному экземпляру на источник → параллельный приём.
/// </summary>
public sealed class ExchangeIngestService : BackgroundService
{
    private readonly IExchangeClient _client;
    private readonly ITickParser _parser;
    private readonly INormalizer _normalizer;
    private readonly IngestPipeline _pipeline;
    private readonly MarketDataMetrics _metrics;
    private readonly ILogger<ExchangeIngestService> _logger;

    public ExchangeIngestService(
        IExchangeClient client,
        ITickParser parser,
        INormalizer normalizer,
        IngestPipeline pipeline,
        MarketDataMetrics metrics,
        ILogger<ExchangeIngestService> logger)
    {
        _client = client;
        _parser = parser;
        _normalizer = normalizer;
        _pipeline = pipeline;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var received = 0L;
        var failed = 0L;

        try
        {
            await foreach (var raw in _client.StreamAsync(stoppingToken))
            {
                if (!_parser.TryParse(raw.Span, _client.Exchange, out var ticks))
                {
                    failed++;
                    _metrics.RecordParseFailure(_client.Exchange);
                    continue;
                }

                foreach (var tick in ticks)
                {
                    Tick normalized;
                    try
                    {
                        normalized = _normalizer.Normalize(tick);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Изоляция сбоев: один кривой тик не должен ронять источник (и хост).
                        failed++;
                        _metrics.RecordParseFailure(_client.Exchange);
                        _logger.LogWarning(ex, "{Exchange}: normalize failed, tick skipped", _client.Exchange);
                        continue;
                    }

                    await _pipeline.Writer.WriteAsync(normalized, stoppingToken);
                    received++;
                    _metrics.RecordReceived(_client.Exchange);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Штатная остановка хоста.
        }

        _logger.LogInformation(
            "{Exchange}: ingest stopped, received={Received}, failedToParse={Failed}",
            _client.Exchange, received, failed);
    }
}
