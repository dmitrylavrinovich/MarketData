using System.Net.WebSockets;
using MarketData.MockExchange.Formats;
using MarketData.MockExchange.Generation;

namespace MarketData.MockExchange.Streaming;

/// <summary>
/// Принимает WebSocket-подключение и шлёт поток тиков в заданном формате с заданным темпом.
/// Один экземпляр генератора на подключение → независимые потоки без общего состояния.
/// </summary>
public sealed class WebSocketTickStream(
    ITickGenerator generator,
    ILogger<WebSocketTickStream> logger)
{
    public async Task RunAsync(
        HttpContext context,
        ITickFormatter formatter,
        int ticksPerSecond,
        CancellationToken ct)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var rate = ticksPerSecond < 1 ? 1 : ticksPerSecond;
        var connectionId = context.Connection.Id;

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation(
            "{Exchange}: client {ConnectionId} connected, rate={Rate}/s", formatter.Exchange, connectionId, rate);

        var sent = 0L;
        var period = TimeSpan.FromSeconds(1.0 / rate);
        using var timer = new PeriodicTimer(period);

        try
        {
            while (socket.State == WebSocketState.Open && await timer.WaitForNextTickAsync(ct))
            {
                var payload = formatter.Format(generator.Next());
                await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, ct);
                sent++;
            }
        }
        catch (OperationCanceledException)
        {
            // Остановка приложения — штатное завершение.
        }
        catch (WebSocketException ex)
        {
            logger.LogInformation("{Exchange}: client {ConnectionId} dropped: {Reason}", formatter.Exchange, connectionId, ex.Message);
        }
        finally
        {
            await CloseQuietlyAsync(socket);
            logger.LogInformation(
                "{Exchange}: client {ConnectionId} disconnected, sent={Sent}", formatter.Exchange, connectionId, sent);
        }
    }

    private static async Task CloseQuietlyAsync(WebSocket socket)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // Сокет уже мёртв — игнорируем.
            }
        }
    }
}
