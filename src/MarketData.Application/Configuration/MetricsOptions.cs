using System.ComponentModel.DataAnnotations;

namespace MarketData.Application.Configuration;

/// <summary>Параметры репорта метрик в лог (секция "Metrics").</summary>
public sealed class MetricsOptions
{
    public const string SectionName = "Metrics";

    /// <summary>Период вывода снапшота счётчиков в лог, сек.</summary>
    [Range(1, 3_600)]
    public int ReportIntervalSeconds { get; set; } = 10;
}
