using System.Collections.Concurrent;

namespace Server.Session;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _connectionToSession = new();  // connectionId -> playerId

    public Session Create(string connectionId, string playerId, string playerName = "")
    {
        var session = new Session 
        { 
            ConnectionId = connectionId, 
            PlayerId = playerId,
            PlayerName = playerName
        };
        _sessions[playerId] = session;
        _connectionToSession[connectionId] = playerId;
        return session;
    }

    public bool TryGet(string playerId, out Session? session)
        => _sessions.TryGetValue(playerId, out session);

    public bool TryGetByConnection(string connectionId, out Session? session)
    {
        if (_connectionToSession.TryGetValue(connectionId, out var playerId))
        {
            return _sessions.TryGetValue(playerId, out session);
        }
        session = null;
        return false;
    }

    public void Remove(string playerId)
    {
        if (_sessions.TryRemove(playerId, out var session))
        {
            _connectionToSession.TryRemove(session.ConnectionId, out _);
        }
    }

    public void RemoveByConnection(string connectionId)
    {
        if (_connectionToSession.TryRemove(connectionId, out var playerId))
        {
            _sessions.TryRemove(playerId, out _);
        }
    }

    public Session? GetOrCreate(string connectionId, string playerId)
    {
        if (_sessions.TryGetValue(playerId, out var existing))
        {
            // 重连：更新 ConnectionId
            _connectionToSession.TryRemove(existing.ConnectionId, out _);
            _connectionToSession[connectionId] = playerId;
            
            // 重新创建 Session 以更新 ConnectionId（init 属性只能在构造时设置）
            var updatedSession = new Session
            {
                ConnectionId = connectionId,
                PlayerId = existing.PlayerId,
                PlayerName = existing.PlayerName,
                State = existing.State,
                RoomId = existing.RoomId,
                IsDisconnected = false,
                DisconnectTime = null,
                LastHeartbeat = DateTime.UtcNow
            };
            _sessions[playerId] = updatedSession;
            return updatedSession;
        }
        
        return Create(connectionId, playerId);
    }

    public IEnumerable<Session> GetAllSessions() => _sessions.Values;

    public bool TryGetByRoom(string roomId, out List<Session> sessions)
    {
        sessions = _sessions.Values
            .Where(s => s.RoomId == roomId)
            .ToList();

        return sessions.Count > 0;
    }
}
