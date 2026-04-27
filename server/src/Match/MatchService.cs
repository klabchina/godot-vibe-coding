using System.Collections.Concurrent;
using Server.Room;
using Server.Session;

namespace Server.Match;

/// <summary>
/// 匹配服务 — 队列满 2 人即创建房间并绑定连接 ID
/// </summary>
public sealed class MatchService
{
    private readonly ConcurrentQueue<MatchEntry> _queue = new();
    private readonly SessionManager _sessionManager;
    private readonly RoomManager _roomManager;

    public MatchService(SessionManager sessionManager, RoomManager roomManager)
    {
        _sessionManager = sessionManager;
        _roomManager = roomManager;
    }

    public void Enqueue(string playerId, string playerName)
    {
        _queue.Enqueue(new MatchEntry(playerId, playerName));
        Console.WriteLine($"[Match] {playerId} ({playerName}) queued. Size={_queue.Count}");
    }

    public void Dequeue(string playerId)
    {
        Console.WriteLine($"[Match] {playerId} cancelled");
        // ConcurrentQueue 不支持直接移除，实际生产中可改用有序集合
    }

    public void Tick()
    {
        if (_queue.Count < 2) return;

        if (!_queue.TryDequeue(out var e1)) return;
        if (!_queue.TryDequeue(out var e2))
        {
            _queue.Enqueue(e1);
            return;
        }

        if (!_sessionManager.TryGet(e1.PlayerId, out var s1) ||
            !_sessionManager.TryGet(e2.PlayerId, out var s2))
        {
            Console.WriteLine("[Match] Session not found, aborting match");
            return;
        }

        // 创建房间
        var room = _roomManager.CreateRoom(e1.PlayerId, e2.PlayerId);

        // 绑定各玩家的 WebSocket 连接 ID
        room.SetConnection(e1.PlayerId, s1!.ConnectionId);
        room.SetConnection(e2.PlayerId, s2!.ConnectionId);

        // 更新会话状态
        s1.RoomId = room.RoomId;
        s2.RoomId = room.RoomId;
        s1.State = SessionState.InRoom;
        s2.State = SessionState.InRoom;

        Console.WriteLine($"[Match] Matched: Room={room.RoomId}  {e1.PlayerId} vs {e2.PlayerId}");
    }

    private sealed record MatchEntry(string PlayerId, string PlayerName);
}
