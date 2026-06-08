using MarketData.Application.Abstractions;
using MarketData.Application.Pipeline;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Exchange;

/// <summary>
/// Producer одного источника: читает сырой поток клиента, парсит по формату биржи,
/// нормализует и пишет в канал пайплайна. По одному экземпляру на источник → параллельный приём.
/// </summary>
public sealed class ExchangeIngestService : BackgroundService
{
    private readonly IExchangeClient _client;
    private readonly ITickParser _parser;
    private readonly INormalizer _normalizer;
    private readonly IngestPipeline _pipeline;
    private readonly ILogger<ExchangeIngestService> _logger;

    public ExchangeIngestService(
        IExchangeClient client,
        IEnumerable<ITickParser> parsers,
        INormalizer normalizer,
        IngestPipeline pipeline,
        ILogger<ExchangeIngestService> logger)
    {
        _client = client;
        _parser = parsers.FirstOrDefault(p => p.Exchange == client.Exchange)
            ?? throw new InvalidOperationException($"No parser registered for exchange '{client.Exchange}'.");
        _normalizer = normalizer;
        _pipeline = pipeline;
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
                if (!_parser.TryParse(raw.Span, out var ticks))
                {
                    failed++;
                    continue;
                }

                foreach (var tick in ticks)
                {
                    var normalized = _normalizer.Normalize(tick);
                    await _pipeline.Writer.WriteAsync(normalized, stoppingToken);
                    received++;
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
