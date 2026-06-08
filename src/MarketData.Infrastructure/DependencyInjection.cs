using MarketData.Application.Abstractions;
using MarketData.Infrastructure.Parsing;
using Microsoft.Extensions.DependencyInjection;

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
}
