using System.Text;
using MarketData.Infrastructure.Parsing;

namespace MarketData.UnitTests.Infrastructure;

/// <summary>
/// Парсер Exchange C (CSV-like, unix-секунды): валидные строки, неверное число полей,
/// нечисловые значения, нарушение инвариантов.
/// </summary>
public class CsvTickParserTests
{
    private readonly CsvTickParser _parser = new();

    private static ReadOnlySpan<byte> Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Exchange_IsExchangeC() => Assert.Equal("ExchangeC", _parser.Exchange);

    /// <summary>Корректная строка → тик; символ в исходной слэш-форме, время из unix-секунд.</summary>
    [Fact]
    public void TryParse_ValidLine_ReturnsTick()
    {
        var ok = _parser.TryParse(Bytes("BTC/USDT;64250.50;1.2;1749225301"), out var ticks);

        Assert.True(ok);
        var tick = Assert.Single(ticks);
        Assert.Equal("BTC/USDT", tick.Ticker);
        Assert.Equal(64250.50m, tick.Price);
        Assert.Equal(1.2m, tick.Volume);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1749225301), tick.Timestamp);
    }

    /// <summary>Неверное число полей → false.</summary>
    [Theory]
    [InlineData("BTC/USDT;64250.50;1.2")]
    [InlineData("BTC/USDT;64250.50;1.2;1749225301;extra")]
    [InlineData("")]
    public void TryParse_WrongFieldCount_ReturnsFalse(string raw)
    {
        Assert.False(_parser.TryParse(Bytes(raw), out var ticks));
        Assert.Empty(ticks);
    }

    /// <summary>Нечисловая цена → false.</summary>
    [Fact]
    public void TryParse_NonNumericPrice_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(Bytes("BTC/USDT;abc;1.2;1749225301"), out _));
    }

    /// <summary>Нечисловой timestamp → false.</summary>
    [Fact]
    public void TryParse_NonNumericTimestamp_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(Bytes("BTC/USDT;100;1.2;notanumber"), out _));
    }

    /// <summary>Пустой символ → false.</summary>
    [Fact]
    public void TryParse_EmptySymbol_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(Bytes(";100;1.2;1749225301"), out _));
    }
}
