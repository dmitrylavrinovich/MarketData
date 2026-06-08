namespace MarketData.MockExchange.Configuration;

/// <summary>Настройки генератора нагрузки mock-биржи (секция "MockExchange").</summary>
public sealed class MockExchangeOptions
{
    public const string SectionName = "MockExchange";

    /// <summary>Инструменты в канонической форме "BASE-QUOTE".</summary>
    public string[] Symbols { get; set; } = ["BTC-USDT", "ETH-USDT", "SOL-USDT"];

    /// <summary>Темп генерации на одно подключение, тиков/сек. Переопределяется query-параметром <c>?rate=</c>.</summary>
    public int TicksPerSecondPerStream { get; set; } = 30;
}
