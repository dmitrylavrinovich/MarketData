using System.ComponentModel.DataAnnotations;

namespace MarketData.Application.Configuration;

/// <summary>Параметры переподключения к бирже: exponential backoff (секция "Reconnect").</summary>
public sealed class ReconnectOptions
{
    public const string SectionName = "Reconnect";

    /// <summary>Стартовая задержка перед реконнектом, мс.</summary>
    [Range(1, 60_000)]
    public int BaseDelayMs { get; set; } = 500;

    /// <summary>Потолок задержки, мс. Backoff растёт от <see cref="BaseDelayMs"/> до этого значения.</summary>
    [Range(1, 600_000)]
    public int MaxDelayMs { get; set; } = 30_000;

    /// <summary>
    /// Watchdog: если от источника нет данных дольше этого времени — соединение считается зависшим
    /// и принудительно переподключается. 0 — watchdog выключен.
    /// </summary>
    [Range(0, 3_600)]
    public int IdleTimeoutSeconds { get; set; } = 30;
}
