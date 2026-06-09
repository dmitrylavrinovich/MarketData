using System.Text;
using MarketData.Infrastructure.Parsing;

namespace MarketData.UnitTests.Infrastructure;

/// <summary>
/// Парсер CSV-like (unix-секунды): валидные строки, неверное число полей, edge cases.
/// </summary>
public class CsvTickParserTests
{
    private readonly CsvTickParser _parser = new();
    private const string Exchange = "ExchangeC";

    private static ReadOnlySpan<byte> Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void ParserKind_IsCsv() => Assert.Equal(CsvTickParser.Kind, _parser.ParserKind);

    [Fact]
    public void TryParse_ValidLine_ReturnsTick()
    {
        var ok = _parser.TryParse(Bytes("BTC/USDT;64250.50;1.2;1749225301"), Exchange, out var ticks);

        Assert.True(ok);
        var tick = Assert.Single(ticks);
        Assert.Equal(Exchange, tick.Exchange);
        Assert.Equal("BTC/USDT", tick.Ticker);
        Assert.Equal(64250.50m, tick.Price);
        Assert.Equal(1.2m, tick.Volume);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1749225301), tick.Timestamp);
    }

    [Theory]
    [InlineData("BTC/USDT;64250.50;1.2")]
    [InlineData("BTC/USDT;64250.50;1.2;1749225301;extra")]
    [InlineData("")]
    public void TryParse_WrongFieldCount_ReturnsFalse(string raw)
    {
        Assert.False(_parser.TryParse(Bytes(raw), Exchange, out var ticks));
        Assert.Empty(ticks);
    }

    [Fact]
    public void TryParse_NonNumericPrice_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(Bytes("BTC/USDT;abc;1.2;1749225301"), Exchange, out _));
    }

    [Fact]
    public void TryParse_NonNumericTimestamp_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(Bytes("BTC/USDT;100;1.2;notanumber"), Exchange, out _));
    }

    [Fact]
    public void TryParse_EmptySymbol_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(Bytes(";100;1.2;1749225301"), Exchange, out _));
    }
}
