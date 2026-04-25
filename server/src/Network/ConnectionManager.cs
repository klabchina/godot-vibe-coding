using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace Server.Network;

/// <summary>
/// 连接管理器
/// </summary>
public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new();

    public string Add(WebSocket socket)
    {
        var id = Guid.NewGuid().ToString("N");
        _connections[id] = socket;
        _lastActivity[id] = DateTime.UtcNow;
        return id;
    }

    public bool TryGet(string connectionId, out WebSocket? socket)
    {
        _lastActivity.TryGetValue(connectionId, out _);
        return _connections.TryGetValue(connectionId, out socket);
    }

    public bool Remove(string connectionId)
    {
        _lastActivity.TryRemove(connectionId, out _);
        return _connections.TryRemove(connectionId, out _);
    }

    public int Count => _connections.Count;

    public void UpdateActivity(string connectionId)
    {
        _lastActivity[connectionId] = DateTime.UtcNow;
    }

    /// <summary>
    /// 获取超时的连接
    /// </summary>
    public IEnumerable<string> GetTimedOutConnections(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _lastActivity)
        {
            if (now - kvp.Value > timeout)
            {
                yield return kvp.Key;
            }
        }
    }
}
