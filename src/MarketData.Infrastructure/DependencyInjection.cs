using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;
using MarketData.Application.Pipeline;
using MarketData.Infrastructure.Exchange;
using MarketData.Infrastructure.Parsing;
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
                sp.GetRequiredService<ILogger<ExchangeIngestService>>()));
        }

        return services;
    }
}
