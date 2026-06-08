using MarketData.Application.Configuration;

namespace MarketData.Infrastructure.Exchange;

/// <summary>
/// Экспоненциальный backoff с full jitter для реконнекта.
/// Базовая задержка удваивается с каждой неудачной попыткой до потолка, затем берётся
/// случайное значение в диапазоне [delay/2, delay] — jitter защищает от thundering herd,
/// когда несколько источников падают и переподключаются одновременно.
/// </summary>
public static class ReconnectBackoff
{
    public static int NextDelayMs(int attempt, ReconnectOptions options, Random random)
    {
        // attempt считается от 0; ограничиваем степень, чтобы Pow не переполнился.
        var exponent = Math.Min(attempt, 30);
        var ceiling = options.BaseDelayMs * Math.Pow(2, exponent);
        var capped = Math.Min(ceiling, options.MaxDelayMs);

        var half = capped / 2;
        var jittered = half + random.NextDouble() * half;

        return (int)Math.Max(jittered, 1);
    }
}
