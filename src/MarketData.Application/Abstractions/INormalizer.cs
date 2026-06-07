using MarketData.Domain.Entities;

namespace MarketData.Application.Abstractions;

/// <summary>
/// Приводит тик к каноническому виду: нормализация символа (напр. "BTCUSDT", "BTC/USDT" → "BTC-USDT").
/// Время уже нормализуется к UTC в <see cref="Tick"/>; здесь — доменная канонизация полей.
/// </summary>
public interface INormalizer
{
    Tick Normalize(Tick raw);
}
