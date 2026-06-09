using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;
using MarketData.Infrastructure.Parsing;

namespace MarketData.UnitTests.Infrastructure;

public class TickParserSelectorTests
{
    private static readonly ITickParser[] Parsers =
    [
        new JsonSnakeTickParser(),
        new JsonNestedTickParser(),
        new CsvTickParser(),
    ];

    [Theory]
    [InlineData("JsonSnake", typeof(JsonSnakeTickParser))]
    [InlineData("JsonNested", typeof(JsonNestedTickParser))]
    [InlineData("Csv", typeof(CsvTickParser))]
    public void Select_KnownParserKind_ReturnsParser(string kind, Type expectedType)
    {
        var exchange = new ExchangeOptions { Name = "AnyName", Url = "ws://x", Parser = kind };

        var parser = TickParserSelector.Select(Parsers, exchange);

        Assert.IsType(expectedType, parser);
    }

    [Fact]
    public void Select_UnknownParserKind_Throws()
    {
        var exchange = new ExchangeOptions { Name = "ExchangeZ", Url = "ws://x", Parser = "Unknown" };

        var ex = Assert.Throws<InvalidOperationException>(() => TickParserSelector.Select(Parsers, exchange));

        Assert.Contains("Unknown", ex.Message);
        Assert.Contains("ExchangeZ", ex.Message);
    }
}
