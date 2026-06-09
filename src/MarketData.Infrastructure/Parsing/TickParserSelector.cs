using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;

namespace MarketData.Infrastructure.Parsing;

/// <summary>Выбор <see cref="ITickParser"/> по <see cref="ExchangeOptions.Parser"/>.</summary>
public static class TickParserSelector
{
    public static ITickParser Select(IEnumerable<ITickParser> parsers, ExchangeOptions exchange) =>
        parsers.FirstOrDefault(p => p.ParserKind == exchange.Parser)
        ?? throw new InvalidOperationException(
            $"No parser registered for Parser '{exchange.Parser}' (exchange '{exchange.Name}').");
}
