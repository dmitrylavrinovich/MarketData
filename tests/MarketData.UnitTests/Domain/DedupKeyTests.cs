using MarketData.Domain.ValueObjects;

namespace MarketData.UnitTests.Domain;

/// <summary>
/// Проверяет value-семантику <see cref="DedupKey"/>: равенство и хеш по всем полям,
/// различие при изменении любого из них (основа корректной дедупликации).
/// </summary>
public class DedupKeyTests
{
    private static DedupKey Create(
        string exchange = "ExchangeA",
        string ticker = "BTC-USDT",
        decimal price = 100m,
        decimal volume = 1m)
        => new(exchange, ticker, DateTimeOffset.UnixEpoch, price, volume);

    /// <summary>Одинаковые поля → ключи равны и дают одинаковый хеш-код.</summary>
    [Fact]
    public void Equals_SameFields_AreEqual()
    {
        Assert.Equal(Create(), Create());
        Assert.Equal(Create().GetHashCode(), Create().GetHashCode());
    }

    /// <summary>Отличие в любом из полей (биржа, тикер, цена, объём) → ключи не равны.</summary>
    [Theory]
    [InlineData("ExchangeB", "BTC-USDT", 100, 1)]
    [InlineData("ExchangeA", "ETH-USDT", 100, 1)]
    [InlineData("ExchangeA", "BTC-USDT", 101, 1)]
    [InlineData("ExchangeA", "BTC-USDT", 100, 2)]
    public void Equals_DifferentField_AreNotEqual(
        string exchange, string ticker, decimal price, decimal volume)
    {
        Assert.NotEqual(Create(), Create(exchange, ticker, price, volume));
    }

    /// <summary>Отличие во времени события → ключи не равны.</summary>
    [Fact]
    public void Equals_DifferentTimestamp_AreNotEqual()
    {
        var a = new DedupKey("ExchangeA", "BTC-USDT", DateTimeOffset.UnixEpoch, 100m, 1m);
        var b = new DedupKey("ExchangeA", "BTC-USDT", DateTimeOffset.UnixEpoch.AddSeconds(1), 100m, 1m);

        Assert.NotEqual(a, b);
    }
}
