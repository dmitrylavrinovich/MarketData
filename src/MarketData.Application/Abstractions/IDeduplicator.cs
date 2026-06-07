using MarketData.Domain.Entities;

namespace MarketData.Application.Abstractions;

/// <summary>
/// Быстрый in-memory фильтр дубликатов (скользящее окно по <see cref="Tick.DedupKey"/>).
/// Первая линия защиты до записи в БД; финальная гарантия — UNIQUE-индекс.
/// </summary>
public interface IDeduplicator
{
    /// <summary><c>true</c>, если тик ранее не встречался (нужно записать); <c>false</c> — дубликат.</summary>
    bool IsNew(in Tick tick);
}
