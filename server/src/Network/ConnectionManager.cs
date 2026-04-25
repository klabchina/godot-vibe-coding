using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace Server.Network;

public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();

    public string Add(WebSocket socket)
    {
        var id = Guid.NewGuid().ToString("N");
        _connections[id] = socket;
        return id;
    }

    public bool TryGet(string connectionId, out WebSocket? socket)
        => _connections.TryGetValue(connectionId, out socket);

    public bool Remove(string connectionId)
        => _connections.TryRemove(connectionId, out _);

    public int Count => _connections.Count;
}
