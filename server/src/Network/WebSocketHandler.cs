using System.Net.WebSockets;
using System.Text;

namespace Server.Network;

public sealed class WebSocketHandler
{
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly MessageRouter _router;
    private readonly ConnectionManager _connections;

    public WebSocketHandler(
        ILogger<WebSocketHandler> logger,
        MessageRouter router,
        ConnectionManager connections)
    {
        _logger = logger;
        _router = router;
        _connections = connections;
    }

    public async Task RunAsync(string connectionId, WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8 * 1024];

        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
                    break;
                }

                var bytes = buffer.AsMemory(0, result.Count);
                await _router.RouteAsync(connectionId, bytes, ct);

                var ack = Encoding.UTF8.GetBytes("ack");
                await socket.SendAsync(ack, WebSocketMessageType.Text, true, ct);
            }
        }
        finally
        {
            _connections.Remove(connectionId);
            _logger.LogInformation("Connection closed: {ConnectionId}", connectionId);
        }
    }
}
