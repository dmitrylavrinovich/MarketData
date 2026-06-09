using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketData.Application.Abstractions;
using MarketData.Domain.Entities;
using MarketData.Domain.Exceptions;

namespace MarketData.Infrastructure.Parsing;

/// <summary>
/// Формат JSON nested (Bybit-like): вложенный объект, цена/объём числами, время — ISO-8601 UTC.
/// Пример: <c>{"topic":"trade","data":{"symbol":"BTC-USDT","price":64250.5,"size":1.2,"timestamp":"2026-06-06T15:55:01.123Z"}}</c>
/// </summary>
public sealed class JsonNestedTickParser : ITickParser
{
    public const string Kind = "JsonNested";

    private static readonly IReadOnlyList<Tick> Empty = Array.Empty<Tick>();

    public string ParserKind => Kind;

    public bool TryParse(ReadOnlySpan<byte> raw, string exchange, out IReadOnlyList<Tick> ticks)
    {
        ticks = Empty;

        NestedDto? dto;
        try { dto = JsonSerializer.Deserialize<NestedDto>(raw); }
        catch (JsonException) { return false; }

        var data = dto?.Data;
        if (data is null || string.IsNullOrWhiteSpace(data.Symbol))
            return false;
        if (!DateTimeOffset.TryParse(
                data.Timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var timestamp))
            return false;

        try
        {
            ticks = [new Tick(exchange, data.Symbol!, data.Price, data.Size, timestamp, DateTimeOffset.UtcNow)];
            return true;
        }
        catch (InvalidTickException)
        {
            return false;
        }
    }

    private sealed record NestedDto(
        [property: JsonPropertyName("topic")] string? Topic,
        [property: JsonPropertyName("data")] NestedData? Data);

    private sealed record NestedData(
        [property: JsonPropertyName("symbol")] string? Symbol,
        [property: JsonPropertyName("price")] decimal Price,
        [property: JsonPropertyName("size")] decimal Size,
        [property: JsonPropertyName("timestamp")] string? Timestamp);
}
