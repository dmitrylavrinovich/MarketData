using System.Text;
using MarketData.Application.Configuration;
using MarketData.Infrastructure.Exchange;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarketData.IntegrationTests;

/// <summary>
/// Реконнект WS-клиента: сервер рвёт соединение после каждого сообщения, клиент должен
/// переподключиться и продолжить приём. Не требует Postgres/Docker — поднимает локальный WS-сервер.
/// </summary>
public sealed class ReconnectIntegrationTests
{
    [Fact]
    public async Task Client_ReconnectsAndKeepsReceiving_AfterServerDropsConnection()
    {
        await using var server = new TestWebSocketServer();
        server.Start();

        var options = Options.Create(new ReconnectOptions
        {
            BaseDelayMs = 50,
            MaxDelayMs = 200,
            IdleTimeoutSeconds = 0, // watchdog не нужен — рвёт сам сервер
        });
        var client = new ExchangeWebSocketClient(
            new ExchangeConnection("ExchangeA", server.Url),
            options,
            NullLogger<ExchangeWebSocketClient>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var received = new List<string>();

        try
        {
            await foreach (var message in client.StreamAsync(cts.Token))
            {
                received.Add(Encoding.UTF8.GetString(message.Span));
                if (received.Count >= 3)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // таймаут защиты от зависания
        }

        // 3 сообщения = клиент пережил минимум 2 обрыва и переподключился.
        Assert.True(received.Count >= 3, $"получено сообщений: {received.Count}");
        Assert.True(server.Connections >= 3, $"подключений к серверу: {server.Connections}");
    }
}
