using MarketData.Domain.Entities;
using MarketData.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarketData.IntegrationTests;

/// <summary>
/// Запись батча в реальный PostgreSQL и дедуп на уровне БД (ON CONFLICT DO NOTHING)
/// по composite-PK (exchange, ticker, ts, price, volume).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EfCoreTickSinkTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Ts = new(2026, 6, 8, 12, 0, 0, TimeSpan.Zero);

    private static Tick Tick(string ticker, decimal price, int msOffset = 0)
        => new("ExchangeA", ticker, price, 1m, Ts.AddMilliseconds(msOffset), Ts);

    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WriteBatchAsync_PersistsAllRows()
    {
        var sink = new EfCoreTickSink(fixture.ContextFactory);
        var batch = new[]
        {
            Tick("BTC-USDT", 64000m, 0),
            Tick("ETH-USDT", 3000m, 1),
            Tick("SOL-USDT", 150m, 2),
        };

        await sink.WriteBatchAsync(batch, CancellationToken.None);

        Assert.Equal(3, await CountAsync());
    }

    [Fact]
    public async Task WriteBatchAsync_DuplicateAcrossBatches_DoesNotInsertSecondRow()
    {
        var sink = new EfCoreTickSink(fixture.ContextFactory);
        var tick = Tick("BTC-USDT", 64000m);

        await sink.WriteBatchAsync([tick], CancellationToken.None);
        // Тот же дедуп-ключ во втором батче (модель рестарта / гонки) — не должен создать вторую строку.
        await sink.WriteBatchAsync([tick], CancellationToken.None);

        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task WriteBatchAsync_DuplicatesWithinSameBatch_CollapseToOne()
    {
        var sink = new EfCoreTickSink(fixture.ContextFactory);
        var tick = Tick("BTC-USDT", 64000m);

        await sink.WriteBatchAsync([tick, tick], CancellationToken.None);

        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task WriteBatchAsync_DifferentPrice_IsNotDuplicate()
    {
        var sink = new EfCoreTickSink(fixture.ContextFactory);

        // price входит в composite-PK → разная цена = разные строки.
        await sink.WriteBatchAsync([Tick("BTC-USDT", 64000m)], CancellationToken.None);
        await sink.WriteBatchAsync([Tick("BTC-USDT", 64001m)], CancellationToken.None);

        Assert.Equal(2, await CountAsync());
    }

    private async Task<int> CountAsync()
    {
        await using var db = await fixture.ContextFactory.CreateDbContextAsync();
        return await db.Ticks.CountAsync();
    }
}
