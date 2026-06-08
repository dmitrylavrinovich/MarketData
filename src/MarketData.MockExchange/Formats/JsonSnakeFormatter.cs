using System.Globalization;
using System.Text.Json;
using MarketData.MockExchange.Generation;

namespace MarketData.MockExchange.Formats;

/// <summary>
/// Exchange A (Binance-like): JSON с короткими ключами, цена/объём строками, время — unix ms.
/// Символ без разделителя: "BTC-USDT" → "BTCUSDT".
/// Пример: <c>{"s":"BTCUSDT","p":"64250.50","q":"1.2","T":1749225301123}</c>
/// </summary>
public sealed class JsonSnakeFormatter : ITickFormatter
{
    public string Exchange => "ExchangeA";

    public ReadOnlyMemory<byte> Format(MarketTick tick)
    {
        var dto = new SnakeDto(
            tick.Symbol.Replace("-", string.Empty),
            tick.Price.ToString(CultureInfo.InvariantCulture),
            tick.Volume.ToString(CultureInfo.InvariantCulture),
            tick.Timestamp.ToUnixTimeMilliseconds());

        return JsonSerializer.SerializeToUtf8Bytes(dto);
    }

    private sealed record SnakeDto(string s, string p, string q, long T);
}
