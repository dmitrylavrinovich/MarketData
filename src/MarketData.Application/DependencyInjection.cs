using MarketData.Application.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace MarketData.Application;

/// <summary>Регистрация сервисов слоя Application. Реализации портов подключает Infrastructure.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Один канал на процесс — общий буфер для всех источников и единственного консьюмера.
        services.AddSingleton<IngestPipeline>();

        return services;
    }
}
