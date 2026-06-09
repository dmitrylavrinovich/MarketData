using System.ComponentModel.DataAnnotations;

namespace MarketData.Application.Configuration;

/// <summary>
/// Описание одного источника из секции "Exchanges". Список биндится в Worker —
/// добавление биржи = строка в конфиге, без перекомпиляции.
/// </summary>
public sealed class ExchangeOptions
{
    public const string SectionName = "Exchanges";

    /// <summary>Логическое имя источника. Попадает в <see cref="Domain.Entities.Tick.Exchange"/> и <see cref="Abstractions.IExchangeClient.Exchange"/>.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>WebSocket-адрес биржи, напр. "ws://localhost:5001/ws".</summary>
    [Required]
    public string Url { get; set; } = string.Empty;
    /// <summary>Идентификатор формата для выбора парсера, напр. "JsonSnake", "JsonNested", "Csv".</summary>
    [Required]
    public string Parser { get; set; } = string.Empty;

}
