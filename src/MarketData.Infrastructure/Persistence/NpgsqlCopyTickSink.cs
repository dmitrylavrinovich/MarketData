using MarketData.Application.Abstractions;
using MarketData.Domain.Entities;

namespace MarketData.Infrastructure.Persistence;

/// <summary>
/// Заглушка. Реализация bulk-записи через PostgreSQL COPY (Npgsql binary import).
/// Активировать при росте нагрузки (десятки тысяч тик/сек): заменить тело на
/// <c>BeginBinaryImportAsync("COPY ticks (...) FROM STDIN (FORMAT BINARY)")</c>
/// и переключить регистрацию в <see cref="DependencyInjection.AddPersistence"/>.
///
/// Для 100 тик/сек COPY избыточен: <see cref="EfCoreTickSink"/> справляется с запасом,
/// а COPY усложняет дедуп (нет прямого ON CONFLICT — нужна staging-таблица).
/// Точка расширения видна в коде; пайплайн при подмене не меняется.
/// </summary>
public sealed class NpgsqlCopyTickSink : ITickSink
{
    public Task WriteBatchAsync(IReadOnlyList<Tick> batch, CancellationToken ct) =>
        throw new NotImplementedException(
            "Stub: заменить на BeginBinaryImportAsync(COPY ... FROM STDIN). См. README § Scaling.");
}
