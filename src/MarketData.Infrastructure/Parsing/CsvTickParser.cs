using System.Globalization;
using System.Text;
using MarketData.Application.Abstractions;
using MarketData.Domain.Entities;
using MarketData.Domain.Exceptions;

namespace MarketData.Infrastructure.Parsing;

/// <summary>
/// Exchange C (legacy): не-JSON, поля через ';', время — unix секунды.
/// Пример: <c>BTC/USDT;64250.50;1.2;1749225301</c>
/// </summary>
public sealed class CsvTickParser : ITickParser
{
    private const int FieldCount = 4;
    private static readonly IReadOnlyList<Tick> Empty = Array.Empty<Tick>();

    public string Exchange => "ExchangeC";

    public bool TryParse(ReadOnlySpan<byte> raw, out IReadOnlyList<Tick> ticks)
    {
        ticks = Empty;

        string line;
        try { line = Encoding.UTF8.GetString(raw); }
        catch (ArgumentException) { return false; }

        var parts = line.Split(';');
        if (parts.Length != FieldCount)
            return false;

        var symbol = parts[0];
        if (string.IsNullOrWhiteSpace(symbol))
            return false;
        if (!decimal.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var price))
            return false;
        if (!decimal.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var volume))
            return false;
        if (!long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
            return false;

        try
        {
            var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            ticks = [new Tick(Exchange, symbol, price, volume, timestamp, DateTimeOffset.UtcNow)];
            return true;
        }
        catch (Exception ex) when (ex is InvalidTickException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
