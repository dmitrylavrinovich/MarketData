using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;
using MarketData.Domain.Entities;
using MarketData.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Deduplication;

/// <summary>
/// In-memory скользящее окно дедупа по <see cref="Tick.DedupKey"/>.
/// Ключ живёт <see cref="DedupOptions.WindowTtlSeconds"/>; при превышении
/// <see cref="DedupOptions.MaxEntries"/> протухшие записи вытесняются.
/// Первая линия защиты до БД; финальная гарантия — UNIQUE-индекс.
/// </summary>
public sealed class InMemoryDeduplicator : IDeduplicator
{
    private readonly Dictionary<DedupKey, long> _seen;
    private readonly TimeProvider _time;
    private readonly long _ttlMs;
    private readonly int _maxEntries;
    private readonly object _gate = new();

    public InMemoryDeduplicator(IOptions<DedupOptions> options, TimeProvider? timeProvider = null)
    {
        var opts = options.Value;
        _ttlMs = opts.WindowTtlSeconds * 1000L;
        _maxEntries = opts.MaxEntries;
        _time = timeProvider ?? TimeProvider.System;
        _seen = new Dictionary<DedupKey, long>(Math.Min(_maxEntries, 1024));
    }

    public bool IsNew(in Tick tick)
    {
        var key = tick.DedupKey;
        var now = _time.GetUtcNow().ToUnixTimeMilliseconds();

        lock (_gate)
        {
            if (_seen.TryGetValue(key, out var expiry) && expiry > now)
                return false; // живой дубликат

            if (_seen.Count >= _maxEntries)
                Evict(now);

            _seen[key] = now + _ttlMs;
            return true;
        }
    }

    /// <summary>Удаляет протухшие записи; если этого мало — самые близкие к истечению.</summary>
    private void Evict(long now)
    {
        var expired = _seen.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList();
        foreach (var key in expired)
            _seen.Remove(key);

        if (_seen.Count < _maxEntries)
            return;

        var toRemove = _seen.Count - _maxEntries + 1;
        var oldest = _seen.OrderBy(kv => kv.Value).Take(toRemove).Select(kv => kv.Key).ToList();
        foreach (var key in oldest)
            _seen.Remove(key);
    }
}
