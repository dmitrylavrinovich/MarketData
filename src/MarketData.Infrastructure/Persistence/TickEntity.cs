using MarketData.Domain.Entities;

namespace MarketData.Infrastructure.Persistence;

/// <summary>
/// EF-проекция доменного <see cref="Tick"/> на таблицу <c>ticks</c>.
/// Композитный ключ = дедуп-набор (exchange, ticker, ts, price, volume):
/// одновременно PK, UNIQUE-гарантия и обязательное для hypertable включение партиционного столбца ts.
/// </summary>
public sealed class TickEntity
{
    public string Exchange { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset IngestedAt { get; set; }

    public static TickEntity FromDomain(Tick tick) => new()
    {
        Exchange = tick.Exchange,
        Ticker = tick.Ticker,
        Price = tick.Price,
        Volume = tick.Volume,
        Timestamp = tick.Timestamp,
        IngestedAt = tick.IngestedAt,
    };
}
