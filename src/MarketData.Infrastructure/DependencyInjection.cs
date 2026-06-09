using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;
using MarketData.Application.Monitoring;
using MarketData.Application.Pipeline;
using MarketData.Infrastructure.Deduplication;
using MarketData.Infrastructure.Exchange;
using MarketData.Infrastructure.Parsing;
using MarketData.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure;

/// <summary>Регистрация реализаций портов слоя Infrastructure.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Парсеры — stateless, резолвятся как IEnumerable<ITickParser> и выбираются по Exchange (Strategy).
        services.AddSingleton<ITickParser, JsonSnakeTickParser>();
        services.AddSingleton<ITickParser, JsonNestedTickParser>();
        services.AddSingleton<ITickParser, CsvTickParser>();

        services.AddSingleton<INormalizer, TickNormalizer>();

        // Дедуп — общий на процесс (единственный consumer), потокобезопасен на случай SingleReader=false.
        services.AddSingleton<IDeduplicator, InMemoryDeduplicator>();

        return services;
    }

    /// <summary>
    /// Регистрирует запись тиков в PostgreSQL. Контекст — через фабрику (sink живёт как singleton).
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<MarketDataDbContext>(options => options.UseNpgsql(connectionString));

        // --- ITickSink: точка подмены при масштабировании ---
        // Сейчас (100 тик/сек): EF Core достаточно.
        services.AddSingleton<ITickSink, EfCoreTickSink>();

        // При росте нагрузки (~50k+ тик/сек): раскомментировать, закомментировать строку выше.
        // services.AddSingleton<ITickSink, NpgsqlCopyTickSink>();

        return services;
    }

    /// <summary>
    /// Регистрирует по одному <see cref="ExchangeIngestService"/> на источник (параллельный приём).
    /// Вызывается из Worker после биндинга списка <see cref="ExchangeOptions"/>.
    /// </summary>
    public static IServiceCollection AddExchangeIngestion(
        this IServiceCollection services, IEnumerable<ExchangeOptions> exchanges)
    {
        foreach (var exchange in exchanges)
        {
            var connection = new ExchangeConnection(exchange.Name, exchange.Url);

            services.AddSingleton<IHostedService>(sp => new ExchangeIngestService(
                new ExchangeWebSocketClient(
                    connection,
                    sp.GetRequiredService<IOptions<ReconnectOptions>>(),
                    sp.GetRequiredService<ILogger<ExchangeWebSocketClient>>()),
                sp.GetRequiredService<IEnumerable<ITickParser>>(),
                sp.GetRequiredService<INormalizer>(),
                sp.GetRequiredService<IngestPipeline>(),
                sp.GetRequiredService<MarketDataMetrics>(),
                sp.GetRequiredService<ILogger<ExchangeIngestService>>()));
        }

        return services;
    }
}
