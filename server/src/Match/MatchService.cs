using Server.Proto;
using Server.Room;
using Server.Session;

namespace Server.Match;

/// <summary>
/// 匹配服务 — 优先加入已有 1 人 Waiting 房间，否则创建新房间
/// </summary>
public sealed class MatchService
{
    private readonly SessionManager _sessionManager;
    private readonly RoomManager _roomManager;

    public MatchService(SessionManager sessionManager, RoomManager roomManager)
    {
        _sessionManager = sessionManager;
        _roomManager = roomManager;
    }

    public MatchSuccess? Enqueue(string playerId, string playerName)
    {
        if (!_sessionManager.TryGet(playerId, out var session) || session == null)
        {
            return null;
        }

        var waitingRoom = _roomManager
            .GetAllRooms()
            .FirstOrDefault(r => r.State == RoomState.Waiting && r.PlayerCount == 1);

        if (waitingRoom == null)
        {
            var newRoom = _roomManager.CreateRoom(playerId);
            newRoom.SetConnection(playerId, session.ConnectionId);
            session.RoomId = newRoom.RoomId;
            session.State = SessionState.InRoom;
            Console.WriteLine($"[Match] Create waiting room={newRoom.RoomId} player={playerId}");
            return null;
        }

        waitingRoom.AddPlayer(playerId);
        waitingRoom.SetConnection(playerId, session.ConnectionId);
        session.RoomId = waitingRoom.RoomId;
        session.State = SessionState.InRoom;

        if (!_sessionManager.TryGetByRoom(waitingRoom.RoomId, out var sessions))
        {
            return null;
        }

        var players = sessions
            .Select((s, idx) => new PlayerInfo
            {
                PlayerId = s.PlayerId,
                PlayerName = s.PlayerName,
                Slot = idx,
            });

        Console.WriteLine($"[Match] Matched room={waitingRoom.RoomId}");

        var result = new MatchSuccess
        {
            RoomId = waitingRoom.RoomId,
        };
        result.Players.AddRange(players);
        return result;
    }

    public bool Cancel(string playerId)
    {
        if (!_sessionManager.TryGet(playerId, out var session) || session?.RoomId == null)
        {
            return false;
        }

        var room = _roomManager.GetRoom(session.RoomId);
        if (room == null || room.State != RoomState.Waiting)
        {
            return false;
        }

        _roomManager.DestroyRoom(room.RoomId);
        session.RoomId = null;
        session.State = SessionState.Idle;
        return true;
    }

    public void Tick()
    {
        // 协议变更后，匹配在 Enqueue 时即时完成
    }
}
