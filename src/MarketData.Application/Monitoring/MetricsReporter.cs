using MarketData.Application.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Application.Monitoring;

/// <summary>
/// Периодически (раз в <see cref="MetricsOptions.ReportIntervalSeconds"/>) пишет снапшот счётчиков
/// в лог — выполняет требование ТЗ «счётчик обработанных тиков в консоль/лог».
/// </summary>
public sealed class MetricsReporter(
    MarketDataMetrics metrics,
    IOptions<MetricsOptions> options,
    ILogger<MetricsReporter> logger) : BackgroundService
{
    private readonly MetricsOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.ReportIntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var s = metrics.Snapshot();
                logger.LogInformation(
                    "metrics: received={Received} written={Written} duplicates={Duplicates} parseFailures={ParseFailures} channelDepth={ChannelDepth}",
                    s.Received, s.Written, s.Duplicates, s.ParseFailures, s.ChannelDepth);
            }
        }
        catch (OperationCanceledException)
        {
            // Штатная остановка хоста.
        }
    }
}
