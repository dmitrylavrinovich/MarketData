using MarketData.Domain.Entities;
using MarketData.Infrastructure.Parsing;

namespace MarketData.UnitTests.Infrastructure;

/// <summary>
/// Канонизация символа к "BASE-QUOTE" из разных конвенций бирж: слитно, слэш, дефис, регистр.
/// </summary>
public class TickNormalizerTests
{
    private readonly TickNormalizer _normalizer = new();

    private static Tick Make(string ticker)
        => new("ExchangeX", ticker, 100m, 1m, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData("BTCUSDT", "BTC-USDT")]   // слитно (Exchange A)
    [InlineData("BTC/USDT", "BTC-USDT")]  // слэш (Exchange C)
    [InlineData("BTC-USDT", "BTC-USDT")]  // уже канон (Exchange B)
    [InlineData("btc-usdt", "BTC-USDT")]  // нижний регистр
    [InlineData("ETHUSDC", "ETH-USDC")]
    [InlineData("SOLUSDT", "SOL-USDT")]
    public void Normalize_CanonicalizesSymbol(string input, string expected)
    {
        var result = _normalizer.Normalize(Make(input));

        Assert.Equal(expected, result.Ticker);
    }

    /// <summary>Прочие поля не меняются при нормализации.</summary>
    [Fact]
    public void Normalize_PreservesOtherFields()
    {
        var original = Make("BTCUSDT");

        var result = _normalizer.Normalize(original);

        Assert.Equal(original.Exchange, result.Exchange);
        Assert.Equal(original.Price, result.Price);
        Assert.Equal(original.Volume, result.Volume);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.IngestedAt, result.IngestedAt);
    }

    /// <summary>Уже канонический символ → возвращается тот же экземпляр (без лишней аллокации).</summary>
    [Fact]
    public void Normalize_AlreadyCanonical_ReturnsSameInstance()
    {
        var original = Make("BTC-USDT");

        var result = _normalizer.Normalize(original);

        Assert.Same(original, result);
    }
}
