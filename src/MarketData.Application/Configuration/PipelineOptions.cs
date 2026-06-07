using System.ComponentModel.DataAnnotations;

namespace MarketData.Application.Configuration;

/// <summary>Параметры пайплайна: ёмкость канала и батчинг записи (секция "Pipeline").</summary>
public sealed class PipelineOptions
{
    public const string SectionName = "Pipeline";

    /// <summary>Ёмкость bounded-канала. При заполнении продьюсеры притормаживают (backpressure).</summary>
    [Range(1, 1_000_000)]
    public int ChannelCapacity { get; set; } = 50_000;

    /// <summary>Максимальный размер батча перед записью в sink.</summary>
    [Range(1, 100_000)]
    public int BatchSize { get; set; } = 500;

    /// <summary>Таймаут набора батча, мс. Батч пишется по достижении <see cref="BatchSize"/> ИЛИ таймаута.</summary>
    [Range(1, 60_000)]
    public int BatchTimeoutMs { get; set; } = 200;
}
