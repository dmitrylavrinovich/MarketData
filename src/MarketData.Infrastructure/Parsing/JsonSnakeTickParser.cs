using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketData.Application.Abstractions;
using MarketData.Domain.Entities;
using MarketData.Domain.Exceptions;

namespace MarketData.Infrastructure.Parsing;

/// <summary>
/// Exchange A (Binance-like): JSON с короткими ключами, цена/объём строками, время — unix ms.
/// Пример: <c>{"s":"BTCUSDT","p":"64250.50","q":"1.2","T":1749225301123}</c>
/// </summary>
public sealed class JsonSnakeTickParser : ITickParser
{
    private static readonly IReadOnlyList<Tick> Empty = Array.Empty<Tick>();

    public string Exchange => "ExchangeA";

    public bool TryParse(ReadOnlySpan<byte> raw, out IReadOnlyList<Tick> ticks)
    {
        ticks = Empty;

        SnakeDto? dto;
        try { dto = JsonSerializer.Deserialize<SnakeDto>(raw); }
        catch (JsonException) { return false; }

        if (dto is null || string.IsNullOrWhiteSpace(dto.Symbol))
            return false;
        if (!decimal.TryParse(dto.Price, NumberStyles.Float, CultureInfo.InvariantCulture, out var price))
            return false;
        if (!decimal.TryParse(dto.Volume, NumberStyles.Float, CultureInfo.InvariantCulture, out var volume))
            return false;

        try
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(dto.UnixMs);
            ticks = [new Tick(Exchange, dto.Symbol!, price, volume, timestamp, DateTimeOffset.UtcNow)];
            return true;
        }
        catch (Exception ex) when (ex is InvalidTickException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private sealed record SnakeDto(
        [property: JsonPropertyName("s")] string? Symbol,
        [property: JsonPropertyName("p")] string? Price,
        [property: JsonPropertyName("q")] string? Volume,
        [property: JsonPropertyName("T")] long UnixMs);
}
