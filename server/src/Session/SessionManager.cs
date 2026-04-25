using System.Collections.Concurrent;

namespace Server.Session;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public Session Create(string connectionId, string playerId)
    {
        var session = new Session { ConnectionId = connectionId, PlayerId = playerId };
        _sessions[playerId] = session;
        return session;
    }

    public bool TryGet(string playerId, out Session? session)
        => _sessions.TryGetValue(playerId, out session);
}
