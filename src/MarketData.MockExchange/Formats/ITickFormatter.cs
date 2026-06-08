using MarketData.MockExchange.Generation;

namespace MarketData.MockExchange.Formats;

/// <summary>
/// Сериализует <see cref="MarketTick"/> в формат конкретной биржи (символ, числа, время).
/// </summary>
public interface ITickFormatter
{
    /// <summary>Имя биржи, чей формат отдаёт этот форматтер.</summary>
    string Exchange { get; }

    /// <summary>UTF-8 представление одного тика для отправки в WebSocket.</summary>
    ReadOnlyMemory<byte> Format(MarketTick tick);
}
