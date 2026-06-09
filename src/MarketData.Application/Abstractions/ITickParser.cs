using MarketData.Domain.Entities;

namespace MarketData.Application.Abstractions;

/// <summary>
/// Парсер wire-формата биржи: сырое сообщение → один или несколько <see cref="Tick"/>.
/// Реализации stateless; выбираются по <see cref="ParserKind"/> из <c>ExchangeOptions.Parser</c> (Strategy).
/// Имя источника (<see cref="IExchangeClient.Exchange"/>) передаётся в <see cref="TryParse"/> при каждом вызове.
/// </summary>
public interface ITickParser
{
    /// <summary>Идентификатор формата, напр. "JsonSnake", "JsonNested", "Csv". Совпадает с <c>ExchangeOptions.Parser</c>.</summary>
    string ParserKind { get; }

    /// <summary>
    /// Пытается распарсить сырое сообщение. <paramref name="exchange"/> — логическое имя источника из конфига.
    /// Возвращает <c>false</c> на битых/частичных данных (без исключения).
    /// </summary>
    bool TryParse(ReadOnlySpan<byte> raw, string exchange, out IReadOnlyList<Tick> ticks);
}
