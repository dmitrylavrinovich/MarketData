using MarketData.Application.Monitoring;
using MarketData.Application.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Application;

/// <summary>Регистрация сервисов слоя Application. Реализации портов подключает Infrastructure.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Один канал на процесс — общий буфер для всех источников и единственного консьюмера.
        services.AddSingleton<IngestPipeline>();

        // Метрики пайплайна (Meter) — общий синглтон для продьюсеров и консьюмера.
        services.AddSingleton<MarketDataMetrics>();

        // Единственный consumer: батчинг + дедуп + запись в ITickSink.
        services.AddHostedService<IngestConsumerService>();

        // Лог-репортер метрик; здесь же привязываем observable-гейдж channel_depth к каналу.
        services.AddSingleton<IHostedService>(sp =>
        {
            var metrics = sp.GetRequiredService<MarketDataMetrics>();
            var pipeline = sp.GetRequiredService<IngestPipeline>();
            metrics.SetChannelDepthProvider(() => pipeline.Depth);

            return new MetricsReporter(
                metrics,
                sp.GetRequiredService<IOptions<Configuration.MetricsOptions>>(),
                sp.GetRequiredService<ILogger<MetricsReporter>>());
        });

        return services;
    }
}
