namespace MarketData.UnitTests.TestSupport;

/// <summary>Управляемое время для тестов TTL: ручная перемотка вперёд.</summary>
public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
