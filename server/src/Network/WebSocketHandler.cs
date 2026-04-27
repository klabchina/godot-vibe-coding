using System.Net.WebSockets;
using System.Text.Json;
using Server.Proto;

namespace Server.Network;

/// <summary>
/// WebSocket 连接的读写工具 — 只负责收发字节，不含游戏逻辑
/// </summary>
public sealed class WebSocketHandler
{
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly ConnectionManager _connections;

    public WebSocketHandler(
        ILogger<WebSocketHandler> logger,
        ConnectionManager connections)
    {
        _logger = logger;
        _connections = connections;
    }

    /// <summary>
    /// 运行 WebSocket 处理循环
    /// </summary>
    public async Task RunAsync(string connectionId, WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8 * 1024];

        try
        {
            _logger.LogInformation("WebSocket connected: {ConnectionId}", connectionId);

            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var bytes = buffer.AsMemory(0, result.Count);
                    // 消息路由由 MessageRouter 处理
                    _connections.UpdateActivity(connectionId);
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error: {ConnectionId}", connectionId);
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        finally
        {
            _connections.Remove(connectionId);
            _logger.LogInformation("WebSocket closed: {ConnectionId}", connectionId);
            
            // 通知断线
            OnDisconnected?.Invoke(connectionId);
        }
    }

    /// <summary>
    /// 发送消息给指定连接
    /// </summary>
    public async Task SendAsync(string connectionId, uint msgId, object message)
    {
        if (!_connections.TryGet(connectionId, out var socket))
        {
            _logger.LogWarning("Connection not found: {ConnectionId}", connectionId);
            return;
        }

        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(message);
            var envelope = BuildEnvelope(msgId, payload);
            await socket.SendAsync(envelope, WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to {ConnectionId}", connectionId);
        }
    }

    /// <summary>
    /// 广播消息给多个连接
    /// </summary>
    public async Task BroadcastAsync(IEnumerable<string> connectionIds, uint msgId, object message)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        var envelope = BuildEnvelope(msgId, payload);

        var tasks = new List<Task>();
        foreach (var connectionId in connectionIds)
        {
            if (_connections.TryGet(connectionId, out var socket) && socket.State == WebSocketState.Open)
            {
                tasks.Add(socket.SendAsync(envelope, WebSocketMessageType.Binary, true, CancellationToken.None));
            }
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 构建消息信封
    /// </summary>
    private byte[] BuildEnvelope(uint msgId, byte[] payload)
    {
        using var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(msgId);
        writer.Write(payload);
        return ms.ToArray();
    }

    /// <summary>
    /// 断线回调
    /// </summary>
    public event Action<string>? OnDisconnected;
}
