using System.Globalization;
using System.Text;
using MarketData.MockExchange.Generation;

namespace MarketData.MockExchange.Formats;

/// <summary>
/// Exchange C (legacy): не-JSON, поля через ';', время — unix секунды.
/// Символ через слэш: "BTC-USDT" → "BTC/USDT".
/// Пример: <c>BTC/USDT;64250.50;1.2;1749225301</c>
/// </summary>
public sealed class CsvFormatter : ITickFormatter
{
    public string Exchange => "ExchangeC";

    public ReadOnlyMemory<byte> Format(MarketTick tick)
    {
        var symbol = tick.Symbol.Replace("-", "/");
        var price = tick.Price.ToString(CultureInfo.InvariantCulture);
        var volume = tick.Volume.ToString(CultureInfo.InvariantCulture);
        var unixSeconds = tick.Timestamp.ToUnixTimeSeconds();

        var line = $"{symbol};{price};{volume};{unixSeconds}";
        return Encoding.UTF8.GetBytes(line);
    }
}
