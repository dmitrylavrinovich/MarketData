using System.Text.Json;
using MarketData.MockExchange.Generation;

namespace MarketData.MockExchange.Formats;

/// <summary>
/// Exchange B (Bybit-like): вложенный JSON, цена/объём числами, время — ISO-8601 UTC.
/// Символ с дефисом: "BTC-USDT".
/// Пример: <c>{"topic":"trade","data":{"symbol":"BTC-USDT","price":64250.5,"size":1.2,"timestamp":"2026-06-06T15:55:01.123Z"}}</c>
/// </summary>
public sealed class JsonNestedFormatter : ITickFormatter
{
    public string Exchange => "ExchangeB";

    public ReadOnlyMemory<byte> Format(MarketTick tick)
    {
        var dto = new NestedDto(
            "trade",
            new NestedData(
                tick.Symbol,
                tick.Price,
                tick.Volume,
                tick.Timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")));

        return JsonSerializer.SerializeToUtf8Bytes(dto);
    }

    private sealed record NestedDto(string topic, NestedData data);

    private sealed record NestedData(string symbol, decimal price, decimal size, string timestamp);
}
