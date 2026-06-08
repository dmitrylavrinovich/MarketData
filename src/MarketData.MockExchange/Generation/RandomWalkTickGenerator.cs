using MarketData.MockExchange.Configuration;
using Microsoft.Extensions.Options;

namespace MarketData.MockExchange.Generation;

/// <summary>
/// Генерирует тики случайным блужданием цены по каждому инструменту.
/// Состояние (последние цены) — на экземпляр, поэтому создаётся по одному на подключение (без блокировок).
/// </summary>
public sealed class RandomWalkTickGenerator : ITickGenerator
{
    private readonly string[] _symbols;
    private readonly Dictionary<string, decimal> _lastPrice;
    private readonly Random _rng = new();

    public RandomWalkTickGenerator(IOptions<MockExchangeOptions> options)
    {
        var configured = options.Value.Symbols.Distinct().ToArray();
        _symbols = configured.Length > 0 ? configured : ["BTC-USDT"];
        _lastPrice = _symbols.ToDictionary(s => s, BasePrice);
    }

    public MarketTick Next()
    {
        var symbol = _symbols[_rng.Next(_symbols.Length)];
        var drift = (decimal)((_rng.NextDouble() - 0.5) * 0.002);
        var price = Math.Round(Math.Max(0.01m, _lastPrice[symbol] * (1m + drift)), 2);
        _lastPrice[symbol] = price;

        var volume = Math.Round((decimal)(_rng.NextDouble() * 2.0 + 0.001), 4);
        return new MarketTick(symbol, price, volume, DateTimeOffset.UtcNow);
    }

    private static decimal BasePrice(string symbol) => symbol switch
    {
        "BTC-USDT" => 64_000m,
        "ETH-USDT" => 3_000m,
        "SOL-USDT" => 150m,
        _ => 100m,
    };
}
