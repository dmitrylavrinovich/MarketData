using MarketData.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MarketData.IntegrationTests;

/// <summary>
/// Поднимает одноразовый PostgreSQL (образ с TimescaleDB) в контейнере на время тестовой сессии,
/// прогоняет реальные EF-миграции (включая создание hypertable) и отдаёт фабрику контекстов.
/// Один контейнер на всю коллекцию — старт контейнера дорогой.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("timescale/timescaledb:2.17.2-pg16")
        .WithDatabase("marketdata")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public IDbContextFactory<MarketDataDbContext> ContextFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        ContextFactory = new SimpleDbContextFactory(options);

        await using var db = await ContextFactory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Очищает таблицу между тестами (изоляция без пересоздания контейнера).</summary>
    public async Task ResetAsync()
    {
        await using var db = await ContextFactory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE ticks");
    }

    private sealed class SimpleDbContextFactory(DbContextOptions<MarketDataDbContext> options)
        : IDbContextFactory<MarketDataDbContext>
    {
        public MarketDataDbContext CreateDbContext() => new(options);
    }
}

/// <summary>Шарит один контейнер Postgres между всеми классами в коллекции.</summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
