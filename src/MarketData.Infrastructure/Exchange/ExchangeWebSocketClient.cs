using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Exchange;

/// <summary>
/// WebSocket-источник на <see cref="ClientWebSocket"/>. Подключается, отдаёт сырые сообщения и
/// при обрыве переподключается с экспоненциальным backoff + jitter. Watchdog принудительно
/// реконнектит зависшее соединение (нет данных дольше <see cref="ReconnectOptions.IdleTimeoutSeconds"/>).
/// </summary>
public sealed class ExchangeWebSocketClient(
    ExchangeConnection connection,
    IOptions<ReconnectOptions> reconnectOptions,
    ILogger<ExchangeWebSocketClient> logger) : IExchangeClient
{
    private const int ReceiveBufferSize = 8 * 1024;
    private const int CloseHandshakeTimeoutSeconds = 2;

    private readonly ReconnectOptions _reconnect = reconnectOptions.Value;

    public string Exchange => connection.Name;

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            using var socket = new ClientWebSocket();
            var connected = await TryConnectAsync(socket, ct);

            if (connected)
            {
                attempt = 0;
                await foreach (var message in ReadMessagesAsync(socket, ct))
                    yield return message;

                await TryCloseAsync(socket);
                logger.LogInformation("{Exchange}: disconnected", Exchange);
            }

            if (ct.IsCancellationRequested)
                yield break;

            var delayMs = ReconnectBackoff.NextDelayMs(attempt++, _reconnect, Random.Shared);
            logger.LogWarning(
                "{Exchange}: reconnecting in {DelayMs} ms (attempt {Attempt})", Exchange, delayMs, attempt);

            if (!await DelayAsync(delayMs, ct))
                yield break;
        }
    }

    private async Task<bool> TryConnectAsync(ClientWebSocket socket, CancellationToken ct)
    {
        try
        {
            await socket.ConnectAsync(new Uri(connection.Url), ct);
            logger.LogInformation("{Exchange}: connected to {Url}", Exchange, connection.Url);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is WebSocketException or UriFormatException)
        {
            logger.LogWarning("{Exchange}: connect failed: {Reason}", Exchange, ex.Message);
            return false;
        }
    }

    private async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadMessagesAsync(
        ClientWebSocket socket,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];
        var idleTimeout = _reconnect.IdleTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(_reconnect.IdleTimeoutSeconds)
            : Timeout.InfiniteTimeSpan;

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var message = new MemoryStream();
            var faultedOrClosed = false;

            while (true)
            {
                var outcome = await ReceiveChunkAsync(socket, buffer, idleTimeout, ct);

                if (outcome.IsIdle)
                {
                    logger.LogWarning(
                        "{Exchange}: no data for {Timeout}s, forcing reconnect",
                        Exchange, _reconnect.IdleTimeoutSeconds);
                    faultedOrClosed = true;
                    break;
                }

                if (!outcome.Ok || outcome.MessageType == WebSocketMessageType.Close)
                {
                    faultedOrClosed = true;
                    break;
                }

                message.Write(buffer, 0, outcome.Count);
                if (outcome.EndOfMessage)
                    break;
            }

            if (faultedOrClosed)
                yield break;

            yield return message.ToArray();
        }
    }

    private static async Task<ReceiveOutcome> ReceiveChunkAsync(
        ClientWebSocket socket, byte[] buffer, TimeSpan idleTimeout, CancellationToken ct)
    {
        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (idleTimeout != Timeout.InfiniteTimeSpan)
            idleCts.CancelAfter(idleTimeout);

        try
        {
            var result = await socket.ReceiveAsync(buffer, idleCts.Token);
            return new ReceiveOutcome(true, result.Count, result.MessageType, result.EndOfMessage, false);
        }
        catch (OperationCanceledException) when (idleCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return ReceiveOutcome.Idle;   // watchdog: молчание дольше idleTimeout
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            return ReceiveOutcome.Failed;
        }
    }

    /// <summary>Best-effort close-handshake. Не привязан к <c>ct</c>, чтобы успеть закрыться при shutdown.</summary>
    private async Task TryCloseAsync(ClientWebSocket socket)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            return;

        try
        {
            using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(CloseHandshakeTimeoutSeconds));
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "client shutdown", closeCts.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // Соединение уже разорвано — закрывать нечего.
        }
    }

    private static async Task<bool> DelayAsync(int delayMs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delayMs, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private readonly record struct ReceiveOutcome(
        bool Ok, int Count, WebSocketMessageType MessageType, bool EndOfMessage, bool IsIdle)
    {
        public static ReceiveOutcome Failed => new(false, 0, WebSocketMessageType.Close, true, false);
        public static ReceiveOutcome Idle => new(false, 0, WebSocketMessageType.Close, true, true);
    }
}
