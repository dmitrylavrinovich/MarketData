using MarketData.Domain.Entities;

namespace MarketData.Application.Abstractions;

/// <summary>
/// Приёмник тиков для персистентного хранения. Пайплайн знает только этот контракт —
/// реализацию (EF Core, COPY) можно сменить через DI без правок оркестрации.
/// </summary>
public interface ITickSink
{
    /// <summary>Атомарно (по возможности) записывает батч тиков. Дубли на уровне БД отсекаются ON CONFLICT.</summary>
    Task WriteBatchAsync(IReadOnlyList<Tick> batch, CancellationToken ct);
}
