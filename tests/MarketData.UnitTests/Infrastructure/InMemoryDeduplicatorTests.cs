using MarketData.Application.Configuration;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Deduplication;
using MarketData.UnitTests.TestSupport;
using Microsoft.Extensions.Options;

namespace MarketData.UnitTests.Infrastructure;

/// <summary>
/// Скользящее окно дедупа: первый тик новый, повтор в окне — дубликат,
/// после истечения TTL ключ снова считается новым; учёт ключа по всем полям.
/// </summary>
public class InMemoryDeduplicatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 8, 12, 0, 0, TimeSpan.Zero);

    private static Tick Tick(decimal price = 100m, decimal volume = 1m)
        => new("ExchangeA", "BTC-USDT", price, volume, Start, Start);

    private static InMemoryDeduplicator Create(FakeTimeProvider time, int ttlSeconds = 300, int maxEntries = 1000)
        => new(Options.Create(new DedupOptions { WindowTtlSeconds = ttlSeconds, MaxEntries = maxEntries }), time);

    [Fact]
    public void IsNew_FirstOccurrence_True()
    {
        var dedup = Create(new FakeTimeProvider(Start));

        Assert.True(dedup.IsNew(Tick()));
    }

    [Fact]
    public void IsNew_DuplicateWithinWindow_False()
    {
        var dedup = Create(new FakeTimeProvider(Start));
        var tick = Tick();

        Assert.True(dedup.IsNew(tick));
        Assert.False(dedup.IsNew(tick));
    }

    [Fact]
    public void IsNew_AfterTtlExpires_TrueAgain()
    {
        var time = new FakeTimeProvider(Start);
        var dedup = Create(time, ttlSeconds: 300);
        var tick = Tick();

        Assert.True(dedup.IsNew(tick));
        time.Advance(TimeSpan.FromSeconds(301));
        Assert.True(dedup.IsNew(tick));
    }

    [Fact]
    public void IsNew_WithinTtlBoundary_StillDuplicate()
    {
        var time = new FakeTimeProvider(Start);
        var dedup = Create(time, ttlSeconds: 300);
        var tick = Tick();

        Assert.True(dedup.IsNew(tick));
        time.Advance(TimeSpan.FromSeconds(299));
        Assert.False(dedup.IsNew(tick));
    }

    [Fact]
    public void IsNew_DifferentPrice_TreatedAsDistinct()
    {
        var dedup = Create(new FakeTimeProvider(Start));

        Assert.True(dedup.IsNew(Tick(price: 100m)));
        Assert.True(dedup.IsNew(Tick(price: 101m)));
    }

    [Fact]
    public void IsNew_EvictsWhenOverCapacity()
    {
        var time = new FakeTimeProvider(Start);
        var dedup = Create(time, ttlSeconds: 1, maxEntries: 2);

        Assert.True(dedup.IsNew(Tick(price: 1m)));
        Assert.True(dedup.IsNew(Tick(price: 2m)));

        // Протухание + переполнение → старые вытесняются, новый ключ влезает без ошибок.
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.True(dedup.IsNew(Tick(price: 3m)));
    }
}
