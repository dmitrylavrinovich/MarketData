using MarketData.Application.Abstractions;
using MarketData.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketData.Infrastructure.Persistence;

/// <summary>
/// Реализация <see cref="ITickSink"/> по умолчанию: batch-вставка через EF Core.
/// Контекст создаётся на батч через <see cref="IDbContextFactory{TContext}"/> — sink живёт как singleton
/// (его потребляет singleton-consumer), без захвата scoped-зависимости и без роста change tracker.
/// </summary>
public sealed class EfCoreTickSink(IDbContextFactory<MarketDataDbContext> contextFactory) : ITickSink
{
    public async Task WriteBatchAsync(IReadOnlyList<Tick> batch, CancellationToken ct)
    {
        if (batch.Count == 0)
            return;

        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        for (var i = 0; i < batch.Count; i++)
            db.Ticks.Add(TickEntity.FromDomain(batch[i]));

        await db.SaveChangesAsync(ct);
    }
}
