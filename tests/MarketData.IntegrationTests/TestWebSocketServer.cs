using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace MarketData.IntegrationTests;

/// <summary>
/// Минимальный WS-сервер для теста реконнекта. На каждое подключение отдаёт одно сообщение
/// (с порядковым номером в цене) и тут же закрывает соединение — провоцирует у клиента обрыв
/// и переподключение. Считает число принятых подключений.
/// </summary>
internal sealed class TestWebSocketServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task _acceptLoop = Task.CompletedTask;
    private int _connections;

    public string Url { get; }

    public int Connections => Volatile.Read(ref _connections);

    public TestWebSocketServer()
    {
        var port = GetFreePort();
        _listener.Prefixes.Add($"http://localhost:{port}/ws/");
        Url = $"ws://localhost:{port}/ws/";
    }

    public void Start()
    {
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }

            if (!ctx.Request.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                continue;
            }

            var seq = Interlocked.Increment(ref _connections);
            _ = HandleConnectionAsync(ctx, seq);
        }
    }

    private async Task HandleConnectionAsync(HttpListenerContext ctx, int seq)
    {
        WebSocket? ws = null;
        try
        {
            ws = (await ctx.AcceptWebSocketAsync(null)).WebSocket;
            var payload = Encoding.UTF8.GetBytes(
                $$"""{"s":"BTCUSDT","p":"{{seq}}.00","q":"1.0","T":1749225301123}""");

            await ws.SendAsync(payload, WebSocketMessageType.Text, true, _cts.Token);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "cycle", _cts.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // Клиент мог уйти раньше — для теста несущественно.
        }
        finally
        {
            ws?.Dispose();
        }
    }

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_listener.IsListening)
            _listener.Stop();

        try
        {
            await _acceptLoop;
        }
        catch
        {
            // accept-loop завершается через отмену/закрытие листенера.
        }

        _listener.Close();
        _cts.Dispose();
    }
}
