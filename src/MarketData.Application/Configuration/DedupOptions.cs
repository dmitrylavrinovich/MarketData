using System.ComponentModel.DataAnnotations;

namespace MarketData.Application.Configuration;

/// <summary>Параметры in-memory дедупа: окно по времени и лимит записей (секция "Dedup").</summary>
public sealed class DedupOptions
{
    public const string SectionName = "Dedup";

    /// <summary>TTL ключа в окне дедупа, сек. Старше — забывается (тик с тем же ключом снова считается новым).</summary>
    [Range(1, 86_400)]
    public int WindowTtlSeconds { get; set; } = 300;

    /// <summary>Верхний предел записей в окне (защита памяти; вытеснение по TTL/размеру).</summary>
    [Range(1, 10_000_000)]
    public int MaxEntries { get; set; } = 200_000;
}
