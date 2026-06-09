using System.Text;
using MarketData.Application.Abstractions;
using MarketData.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence;

/// <summary>
/// Реализация <see cref="ITickSink"/> по умолчанию: batch-вставка через EF Core.
/// Контекст создаётся на батч через <see cref="IDbContextFactory{TContext}"/> — sink живёт как singleton
/// (его потребляет singleton-consumer), без захвата scoped-зависимости и без роста change tracker.
/// Запись через <c>INSERT ... ON CONFLICT DO NOTHING</c>: дубли по composite-PK (дедуп-набор) тихо
/// отбрасываются на уровне БД — гарантия при рестартах и гонке источников, без падения флаша.
/// </summary>
public sealed class EfCoreTickSink(IDbContextFactory<MarketDataDbContext> contextFactory) : ITickSink
{
    private const int ColumnsPerRow = 6;

    public async Task WriteBatchAsync(IReadOnlyList<Tick> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
            return;

        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var (sql, parameters) = BuildInsert(batch);
        await db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }

    /// <summary>
    /// Собирает один многострочный параметризованный INSERT с <c>ON CONFLICT DO NOTHING</c>.
    /// Значения — только через параметры (без конкатенации) во избежание SQL-инъекций.
    /// </summary>
    private static (string Sql, object[] Parameters) BuildInsert(IReadOnlyList<Tick> batch)
    {
        var sb = new StringBuilder(
            "INSERT INTO ticks (exchange, ticker, price, volume, ts, ingested_at) VALUES ");
        var parameters = new object[batch.Count * ColumnsPerRow];

        for (var row = 0; row < batch.Count; row++)
        {
            var b = row * ColumnsPerRow;
            if (row > 0)
                sb.Append(", ");
            sb.Append('(')
              .Append('{').Append(b).Append("}, ")
              .Append('{').Append(b + 1).Append("}, ")
              .Append('{').Append(b + 2).Append("}, ")
              .Append('{').Append(b + 3).Append("}, ")
              .Append('{').Append(b + 4).Append("}, ")
              .Append('{').Append(b + 5).Append("})");

            var tick = batch[row];
            parameters[b] = tick.Exchange;
            parameters[b + 1] = tick.Ticker;
            parameters[b + 2] = tick.Price;
            parameters[b + 3] = tick.Volume;
            parameters[b + 4] = tick.Timestamp;
            parameters[b + 5] = tick.IngestedAt;
        }

        sb.Append(" ON CONFLICT DO NOTHING");
        return (sb.ToString(), parameters);
    }
}
