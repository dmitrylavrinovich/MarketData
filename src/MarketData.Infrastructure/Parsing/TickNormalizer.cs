using MarketData.Application.Abstractions;
using MarketData.Domain.Entities;

namespace MarketData.Infrastructure.Parsing;

/// <summary>
/// Канонизирует символ к виду "BASE-QUOTE" независимо от конвенции биржи:
/// "BTCUSDT" (слитно), "BTC/USDT" (слэш), "btc-usdt" (регистр) → "BTC-USDT".
/// Время уже в UTC на уровне <see cref="Tick"/>.
/// </summary>
public sealed class TickNormalizer : INormalizer
{
    // Порядок важен: более длинные котируемые валюты проверяются раньше (USDT перед USD).
    private static readonly string[] QuoteCurrencies =
        ["USDT", "USDC", "BUSD", "TUSD", "USD", "EUR", "BTC", "ETH", "BNB"];

    public Tick Normalize(Tick raw)
    {
        var canonical = Canonicalize(raw.Ticker);
        return canonical == raw.Ticker
            ? raw
            : new Tick(raw.Exchange, canonical, raw.Price, raw.Volume, raw.Timestamp, raw.IngestedAt);
    }

    private static string Canonicalize(string symbol)
    {
        var s = symbol.Trim().ToUpperInvariant();

        if (s.Contains('-'))
            return s;
        if (s.Contains('/'))
            return s.Replace('/', '-');

        foreach (var quote in QuoteCurrencies)
        {
            if (s.Length > quote.Length && s.EndsWith(quote, StringComparison.Ordinal))
                return $"{s[..^quote.Length]}-{quote}";
        }

        return s;
    }
}
