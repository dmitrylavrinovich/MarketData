using MarketData.Domain.Entities;

namespace MarketData.Application.Abstractions;

/// <summary>
/// Парсер формата конкретной биржи: сырое сообщение → один или несколько <see cref="Tick"/>.
/// Реализации (Infrastructure) знают свой формат (JSON snake, JSON nested, CSV).
/// Выбираются по <see cref="Exchange"/> (Strategy).
/// </summary>
public interface ITickParser
{
    /// <summary>Имя биржи, чей формат парсит этот парсер. Должно совпадать с <see cref="IExchangeClient.Exchange"/>.</summary>
    string Exchange { get; }

    /// <summary>
    /// Пытается распарсить сырое сообщение. Возвращает <c>false</c> на битых/частичных данных
    /// (без выброса исключения — устойчивость к мусору в потоке).
    /// </summary>
    /// <param name="raw">Сырые байты одного сообщения.</param>
    /// <param name="ticks">Распарсенные тики при успехе; пустой список при <c>false</c>.</param>
    bool TryParse(ReadOnlySpan<byte> raw, out IReadOnlyList<Tick> ticks);
}
