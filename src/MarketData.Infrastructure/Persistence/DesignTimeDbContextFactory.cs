using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MarketData.Infrastructure.Persistence;

/// <summary>
/// Фабрика для `dotnet ef` (миграции в design-time, без запуска Worker).
/// Строка подключения здесь нужна только инструменту для генерации DDL, не для runtime.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MarketDataDbContext>
{
    public MarketDataDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseNpgsql("Host=localhost;Database=marketdata;Username=postgres;Password=postgres")
            .Options;

        return new MarketDataDbContext(options);
    }
}
