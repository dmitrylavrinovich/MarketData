using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using MarketData.Application.Abstractions;
using MarketData.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Exchange;

/// <summary>
/// WebSocket-источник на <see cref="ClientWebSocket"/>. Подключается, отдаёт сырые сообщения
/// и при обрыве переподключается с экспоненциальной задержкой (базовый reconnect; jitter/heartbeat — позже).
/// </summary>
public sealed class ExchangeWebSocketClient(
    ExchangeConnection connection,
    IOptions<ReconnectOptions> reconnectOptions,
    ILogger<ExchangeWebSocketClient> logger) : IExchangeClient
{
    private const int ReceiveBufferSize = 8 * 1024;

    private readonly ReconnectOptions _reconnect = reconnectOptions.Value;

    public string Exchange => connection.Name;

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var delayMs = _reconnect.BaseDelayMs;

        while (!ct.IsCancellationRequested)
        {
            using var socket = new ClientWebSocket();
            var connected = await TryConnectAsync(socket, ct);

            if (connected)
            {
                delayMs = _reconnect.BaseDelayMs;
                await foreach (var message in ReadMessagesAsync(socket, ct))
                    yield return message;

                logger.LogInformation("{Exchange}: disconnected", Exchange);
            }

            if (ct.IsCancellationRequested)
                yield break;

            if (!await DelayAsync(delayMs, ct))
                yield break;

            delayMs = Math.Min(delayMs * 2, _reconnect.MaxDelayMs);
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

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var message = new MemoryStream();
            var faultedOrClosed = false;

            while (true)
            {
                var outcome = await ReceiveChunkAsync(socket, buffer, ct);
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
        ClientWebSocket socket, byte[] buffer, CancellationToken ct)
    {
        try
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            return new ReceiveOutcome(true, result.Count, result.MessageType, result.EndOfMessage);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            return ReceiveOutcome.Failed;
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
        bool Ok, int Count, WebSocketMessageType MessageType, bool EndOfMessage)
    {
        public static ReceiveOutcome Failed => new(false, 0, WebSocketMessageType.Close, true);
    }
}
