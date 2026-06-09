using System.Text;
using MarketData.Infrastructure.Parsing;

namespace MarketData.UnitTests.Infrastructure;

/// <summary>
/// Парсер JSON snake: валидные сообщения, битый JSON, отсутствующие поля,
/// нечисловые цена/объём, нарушение доменных инвариантов.
/// </summary>
public class JsonSnakeTickParserTests
{
    private readonly JsonSnakeTickParser _parser = new();
    private const string Exchange = "ExchangeA";

    private static ReadOnlySpan<byte> Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void ParserKind_IsJsonSnake() => Assert.Equal(JsonSnakeTickParser.Kind, _parser.ParserKind);

    [Fact]
    public void TryParse_ValidMessage_ReturnsTickWithExchangeFromArgument()
    {
        var ok = _parser.TryParse(
            Bytes("""{"s":"BTCUSDT","p":"64250.50","q":"1.2","T":1749225301123}"""),
            Exchange,
            out var ticks);

        Assert.True(ok);
        var tick = Assert.Single(ticks);
        Assert.Equal(Exchange, tick.Exchange);
        Assert.Equal("BTCUSDT", tick.Ticker);
        Assert.Equal(64250.50m, tick.Price);
        Assert.Equal(1.2m, tick.Volume);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1749225301123), tick.Timestamp);
    }

    [Fact]
    public void TryParse_CustomExchangeName_UsesArgumentNotHardcoded()
    {
        var ok = _parser.TryParse(
            Bytes("""{"s":"BTCUSDT","p":"100","q":"1","T":1749225301123}"""),
            "MyBinance",
            out var ticks);

        Assert.True(ok);
        Assert.Equal("MyBinance", Assert.Single(ticks).Exchange);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"s\":\"BTCUSDT\",")]
    [InlineData("")]
    public void TryParse_BrokenJson_ReturnsFalse(string raw)
    {
        Assert.False(_parser.TryParse(Bytes(raw), Exchange, out var ticks));
        Assert.Empty(ticks);
    }

    [Fact]
    public void TryParse_MissingSymbol_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(Bytes("""{"p":"100","q":"1","T":1749225301123}"""), Exchange, out _));
    }

    [Fact]
    public void TryParse_NonNumericPrice_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(
            Bytes("""{"s":"BTCUSDT","p":"abc","q":"1","T":1749225301123}"""), Exchange, out _));
    }

    [Fact]
    public void TryParse_ZeroPrice_ReturnsFalse()
    {
        Assert.False(_parser.TryParse(
            Bytes("""{"s":"BTCUSDT","p":"0","q":"1","T":1749225301123}"""), Exchange, out _));
    }
}
