using MarketData.Domain.Entities;
using MarketData.Domain.Exceptions;

namespace MarketData.UnitTests.Domain;

/// <summary>
/// Проверяет инварианты <see cref="Tick"/>: валидация полей, нормализация времени в UTC,
/// value-равенство и формирование ключа дедупа.
/// </summary>
public class TickTests
{
    private static Tick CreateValid(
        string exchange = "ExchangeA",
        string ticker = "BTC-USDT",
        decimal price = 64250.50m,
        decimal volume = 1.2m,
        DateTimeOffset? timestamp = null,
        DateTimeOffset? ingestedAt = null)
        => new(
            exchange,
            ticker,
            price,
            volume,
            timestamp ?? DateTimeOffset.UnixEpoch,
            ingestedAt ?? DateTimeOffset.UnixEpoch);

    /// <summary>Корректные значения → тик создаётся, поля сохранены как переданы.</summary>
    [Fact]
    public void Ctor_WithValidValues_CreatesTick()
    {
        var tick = CreateValid();

        Assert.Equal("ExchangeA", tick.Exchange);
        Assert.Equal("BTC-USDT", tick.Ticker);
        Assert.Equal(64250.50m, tick.Price);
        Assert.Equal(1.2m, tick.Volume);
    }

    /// <summary>Нулевой объём допустим (update-тики), исключения нет.</summary>
    [Fact]
    public void Ctor_WithZeroVolume_IsAllowed()
    {
        var tick = CreateValid(volume: 0m);

        Assert.Equal(0m, tick.Volume);
    }

    /// <summary>Пустая/пробельная/null биржа → <see cref="InvalidTickException"/>.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Ctor_WithEmptyExchange_Throws(string? exchange)
    {
        Assert.Throws<InvalidTickException>(() => CreateValid(exchange: exchange!));
    }

    /// <summary>Пустой/пробельный/null тикер → <see cref="InvalidTickException"/>.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Ctor_WithEmptyTicker_Throws(string? ticker)
    {
        Assert.Throws<InvalidTickException>(() => CreateValid(ticker: ticker!));
    }

    /// <summary>Нулевая или отрицательная цена → <see cref="InvalidTickException"/>.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ctor_WithNonPositivePrice_Throws(decimal price)
    {
        Assert.Throws<InvalidTickException>(() => CreateValid(price: price));
    }

    /// <summary>Отрицательный объём → <see cref="InvalidTickException"/>.</summary>
    [Fact]
    public void Ctor_WithNegativeVolume_Throws()
    {
        Assert.Throws<InvalidTickException>(() => CreateValid(volume: -0.1m));
    }

    /// <summary>Время события с ненулевым offset приводится к UTC (offset 0), instant сохранён.</summary>
    [Fact]
    public void Ctor_NormalizesTimestampToUtc()
    {
        var local = new DateTimeOffset(2026, 6, 6, 18, 0, 0, TimeSpan.FromHours(3));

        var tick = CreateValid(timestamp: local);

        Assert.Equal(TimeSpan.Zero, tick.Timestamp.Offset);
        Assert.Equal(local.UtcDateTime, tick.Timestamp.UtcDateTime);
    }

    /// <summary>Время приёма приводится к UTC (offset 0).</summary>
    [Fact]
    public void Ctor_NormalizesIngestedAtToUtc()
    {
        var local = new DateTimeOffset(2026, 6, 6, 18, 0, 0, TimeSpan.FromHours(3));

        var tick = CreateValid(ingestedAt: local);

        Assert.Equal(TimeSpan.Zero, tick.IngestedAt.Offset);
    }

    /// <summary>Два тика на один и тот же момент, но с разным offset, равны (благодаря UTC-нормализации).</summary>
    [Fact]
    public void Equality_SameInstantDifferentOffset_AreEqual()
    {
        var utc = new DateTimeOffset(2026, 6, 6, 15, 0, 0, TimeSpan.Zero);
        var plus3 = new DateTimeOffset(2026, 6, 6, 18, 0, 0, TimeSpan.FromHours(3));

        var a = CreateValid(timestamp: utc);
        var b = CreateValid(timestamp: plus3);

        Assert.Equal(a, b);
    }

    /// <summary><see cref="Tick.DedupKey"/> содержит ключевые поля тика без искажений.</summary>
    [Fact]
    public void DedupKey_ReflectsCoreFields()
    {
        var ts = new DateTimeOffset(2026, 6, 6, 15, 0, 0, TimeSpan.Zero);
        var tick = CreateValid(timestamp: ts);

        var key = tick.DedupKey;

        Assert.Equal("ExchangeA", key.Exchange);
        Assert.Equal("BTC-USDT", key.Ticker);
        Assert.Equal(ts, key.Timestamp);
        Assert.Equal(64250.50m, key.Price);
        Assert.Equal(1.2m, key.Volume);
    }
}
