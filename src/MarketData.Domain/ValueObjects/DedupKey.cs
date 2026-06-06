namespace MarketData.Domain.ValueObjects;

/// <summary>
/// Ключ дедупликации тика: <c>(Exchange, Ticker, Timestamp, Price, Volume)</c>.
/// Два тика с одинаковым ключом считаются дубликатами.
/// Value-семантика (record struct) — дешёвое сравнение и хеширование для in-memory окна дедупа.
/// </summary>
public readonly record struct DedupKey(
    string Exchange,
    string Ticker,
    DateTimeOffset Timestamp,
    decimal Price,
    decimal Volume);
