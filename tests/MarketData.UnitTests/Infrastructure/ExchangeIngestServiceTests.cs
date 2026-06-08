using System.Runtime.CompilerServices;
using System.Text;
using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;
using MarketData.Application.Pipeline;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Exchange;
using MarketData.Infrastructure.Parsing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarketData.UnitTests.Infrastructure;

/// <summary>
/// Оркестрация producer'а: сырой поток → выбор парсера по Exchange → нормализация → запись в канал.
/// WS-транспорт подменён фейком (реальный сокет — в интеграционных тестах).
/// </summary>
public class ExchangeIngestServiceTests
{
    private static IngestPipeline NewPipeline()
        => new(Options.Create(new PipelineOptions()));

    [Fact]
    public async Task ExecuteAsync_ParsesNormalizesAndWritesToChannel()
    {
        var pipeline = NewPipeline();
        var messages = new[]
        {
            Encoding.UTF8.GetBytes("""{"s":"BTCUSDT","p":"64250.50","q":"1.2","T":1749225301123}"""),
            Encoding.UTF8.GetBytes("""{"s":"ETHUSDT","p":"3000.00","q":"0.5","T":1749225302123}"""),
        };
        var service = new ExchangeIngestService(
            new FakeExchangeClient("ExchangeA", messages),
            [new JsonSnakeTickParser()],
            new TickNormalizer(),
            pipeline,
            NullLogger<ExchangeIngestService>.Instance);

        await service.StartAsync(CancellationToken.None);

        var first = await ReadWithTimeoutAsync(pipeline);
        var second = await ReadWithTimeoutAsync(pipeline);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("BTC-USDT", first.Ticker);   // нормализован из "BTCUSDT"
        Assert.Equal(64250.50m, first.Price);
        Assert.Equal("ETH-USDT", second.Ticker);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsUnparsableMessages()
    {
        var pipeline = NewPipeline();
        var messages = new[]
        {
            Encoding.UTF8.GetBytes("garbage not json"),
            Encoding.UTF8.GetBytes("""{"s":"SOLUSDT","p":"150.00","q":"2.0","T":1749225303123}"""),
        };
        var service = new ExchangeIngestService(
            new FakeExchangeClient("ExchangeA", messages),
            [new JsonSnakeTickParser()],
            new TickNormalizer(),
            pipeline,
            NullLogger<ExchangeIngestService>.Instance);

        await service.StartAsync(CancellationToken.None);

        var tick = await ReadWithTimeoutAsync(pipeline);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("SOL-USDT", tick.Ticker);   // битое сообщение пропущено, валидное прошло
    }

    [Fact]
    public async Task ExecuteAsync_NormalizeThrows_SkipsTickAndContinues()
    {
        var pipeline = NewPipeline();
        var messages = new[]
        {
            Encoding.UTF8.GetBytes("""{"s":"BADUSDT","p":"1.00","q":"1.0","T":1749225301123}"""),
            Encoding.UTF8.GetBytes("""{"s":"ETHUSDT","p":"3000.00","q":"0.5","T":1749225302123}"""),
        };
        var service = new ExchangeIngestService(
            new FakeExchangeClient("ExchangeA", messages),
            [new JsonSnakeTickParser()],
            new ThrowingNormalizer(failForRawContains: "BAD"),
            pipeline,
            NullLogger<ExchangeIngestService>.Instance);

        await service.StartAsync(CancellationToken.None);

        // Первый тик роняет нормализатор — источник не падает, второй валидный доходит.
        var tick = await ReadWithTimeoutAsync(pipeline);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("ETH-USDT", tick.Ticker);
    }

    [Fact]
    public void Ctor_NoMatchingParser_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ExchangeIngestService(
            new FakeExchangeClient("ExchangeZ", []),
            [new JsonSnakeTickParser()],
            new TickNormalizer(),
            NewPipeline(),
            NullLogger<ExchangeIngestService>.Instance));

        Assert.Contains("ExchangeZ", ex.Message);
    }

    private static async Task<Tick> ReadWithTimeoutAsync(IngestPipeline pipeline)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await pipeline.Reader.ReadAsync(cts.Token);
    }

    /// <summary>Нормализует как обычно, но кидает исключение для одного тикера — модель битого тика.</summary>
    private sealed class ThrowingNormalizer(string failForRawContains) : INormalizer
    {
        private readonly TickNormalizer _inner = new();

        public Tick Normalize(Tick raw)
        {
            if (raw.Ticker.Contains(failForRawContains, StringComparison.Ordinal))
                throw new InvalidOperationException($"boom on {raw.Ticker}");
            return _inner.Normalize(raw);
        }
    }

    private sealed class FakeExchangeClient(string exchange, byte[][] messages) : IExchangeClient
    {
        public string Exchange => exchange;

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var message in messages)
            {
                ct.ThrowIfCancellationRequested();
                yield return message;
                await Task.Yield();
            }
        }
    }
}
