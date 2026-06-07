using MarketData.Domain.Exceptions;
using MarketData.Domain.ValueObjects;

namespace MarketData.Domain.Entities;

/// <summary>
/// Нормализованный тик — единый внутренний формат рыночных данных независимо от источника.
/// Immutable. Цена/объём — <see cref="decimal"/> (без потери точности на деньгах),
/// время — в UTC (нормализуется в конструкторе).
/// </summary>
public sealed record Tick
{
    /// <summary>Источник данных (биржа), напр. "ExchangeA".</summary>
    public string Exchange { get; }

    /// <summary>Нормализованный символ инструмента, напр. "BTC-USDT".</summary>
    public string Ticker { get; }

    /// <summary>Цена сделки/котировки. <see cref="decimal"/> — без потери точности на деньгах.</summary>
    public decimal Price { get; }

    /// <summary>Объём в единицах базового актива. Неотрицательный.</summary>
    public decimal Volume { get; }

    /// <summary>Время события на бирже, UTC.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Время приёма тика системой, UTC. Для диагностики лагов.</summary>
    public DateTimeOffset IngestedAt { get; }

    public Tick(
        string exchange,
        string ticker,
        decimal price,
        decimal volume,
        DateTimeOffset timestamp,
        DateTimeOffset ingestedAt)
    {
        if (string.IsNullOrWhiteSpace(exchange))
            throw new InvalidTickException("Exchange must not be empty.");
        if (string.IsNullOrWhiteSpace(ticker))
            throw new InvalidTickException("Ticker must not be empty.");
        if (price <= 0m)
            throw new InvalidTickException($"Price must be positive, got {price}.");
        if (volume < 0m)
            throw new InvalidTickException($"Volume must not be negative, got {volume}.");

        Exchange = exchange;
        Ticker = ticker;
        Price = price;
        Volume = volume;
        Timestamp = timestamp.ToUniversalTime();
        IngestedAt = ingestedAt.ToUniversalTime();
    }

    /// <summary>Ключ дедупликации для in-memory окна и UNIQUE-индекса в БД.</summary>
    public DedupKey DedupKey => new(Exchange, Ticker, Timestamp, Price, Volume);
}
