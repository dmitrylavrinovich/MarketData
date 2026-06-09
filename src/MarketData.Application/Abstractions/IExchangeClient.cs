namespace MarketData.Application.Abstractions;

/// <summary>
/// Источник рыночных данных: подключается к бирже и отдаёт сырой поток сообщений.
/// Реализация (Infrastructure) отвечает за транспорт (WebSocket) и реконнект.
/// Новая биржа = новая реализация без изменения пайплайна (Open/Closed).
/// </summary>
public interface IExchangeClient
{
    /// <summary>Имя источника из <c>ExchangeOptions.Name</c>, напр. "ExchangeA". Парсер выбирается по <c>ExchangeOptions.Parser</c>.</summary>
    string Exchange { get; }

    /// <summary>
    /// Бесконечный поток сырых сообщений биржи. Каждый элемент — одно сообщение «как пришло»
    /// (байты), парсинг — в <see cref="ITickParser"/>. Прекращается при отмене <paramref name="ct"/>.
    /// </summary>
    IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(CancellationToken ct);
}
