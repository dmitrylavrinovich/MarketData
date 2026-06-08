using System.Text;
using MarketData.Infrastructure.Parsing;

namespace MarketData.UnitTests.Infrastructure;

/// <summary>
/// Парсер Exchange B (вложенный JSON, ISO-время): валидные сообщения, битый JSON,
/// отсутствие вложенного объекта, неверное время, нарушение инвариантов.
/// </summary>
public class JsonNestedTickParserTests
{
    private readonly JsonNestedTickParser _parser = new();

    private static ReadOnlySpan<byte> Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Exchange_IsExchangeB() => Assert.Equal("ExchangeB", _parser.Exchange);

    /// <summary>Корректное сообщение → тик; ISO-время приводится к UTC.</summary>
    [Fact]
    public void TryParse_ValidMessage_ReturnsTick()
    {
        var ok = _parser.TryParse(
            Bytes("""{"topic":"trade","data":{"symbol":"BTC-USDT","price":64250.5,"size":1.2,"timestamp":"2026-06-06T15:55:01.123Z"}}"""),
            out var ticks);

        Assert.True(ok);
        var tick = Assert.Single(ticks);
        Assert.Equal("BTC-USDT", tick.Ticker);
        Assert.Equal(64250.5m, tick.Price);
        Assert.Equal(1.2m, tick.Volume);
        Assert.Equal(TimeSpan.Zero, tick.Timestamp.Offset);
        Assert.Equal(new DateTimeOffset(2026, 6, 6, 15, 55, 1, 123, TimeSpan.Zero), tick.Timestamp);
    }

    /// <summary>Время с offset приводится к UTC (instant сохраняется).</summary>
    [Fact]
    public void TryParse_TimestampWithOffset_NormalizedToUtc()
    {
        var ok = _parser.TryParse(
            Bytes("""{"topic":"trade","data":{"symbol":"BTC-USDT","price":100,"size":1,"timestamp":"2026-06-06T18:55:01.123+03:00"}}"""),
            out var ticks);

        Assert.True(ok);
        Assert.Equal(new DateTimeOffset(2026, 6, 6, 15, 55, 1, 123, TimeSpan.Zero), ticks[0].Timestamp);
    }

    /// <summary>Битый JSON → false.</summary>
    [Theory]
    [InlineData("{ broken")]
    [InlineData("")]
    public void TryParse_BrokenJson_ReturnsFalse(string raw)
    {
        Assert.False(_parser.TryParse(Bytes(raw), out _));
    }

    /// <summary>Нет вложенного data → false.</summary>
    [Fact]
    public void TryParse_MissingData_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(Bytes("""{"topic":"trade"}"""), out _));
    }

    /// <summary>Неверный формат времени → false.</summary>
    [Fact]
    public void TryParse_InvalidTimestamp_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(
            Bytes("""{"topic":"trade","data":{"symbol":"BTC-USDT","price":100,"size":1,"timestamp":"not-a-date"}}"""),
            out _));
    }

    /// <summary>Отрицательный объём нарушает инвариант → false.</summary>
    [Fact]
    public void TryParse_NegativeVolume_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(
            Bytes("""{"topic":"trade","data":{"symbol":"BTC-USDT","price":100,"size":-1,"timestamp":"2026-06-06T15:55:01Z"}}"""),
            out _));
    }
}
