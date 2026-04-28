using System.Collections.Concurrent;
using Server.Network;
using Server.Proto;

namespace Server.Room;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly WebSocketHandler _wsHandler;

    public RoomManager(WebSocketHandler wsHandler)
    {
        _wsHandler = wsHandler;
    }

    /// <summary>
    /// 创建房间并订阅帧广播 / 游戏结束事件
    /// </summary>
    public GameRoom CreateRoom(params string[] playerIds)
    {
        var room = new GameRoom(Guid.NewGuid().ToString("N"), playerIds);

        room.OnBroadcastFrame += (connectionIds, frame) =>
        {
            _ = _wsHandler.BroadcastAsync(connectionIds, MsgIds.LockstepFrame, frame);
        };

        room.OnGameStart += (connectionIds, msg) =>
        {
            _ = _wsHandler.BroadcastAsync(connectionIds, MsgIds.GameStart, msg);
        };

        room.OnGameOver += msg =>
        {
            foreach (var connId in room.GetConnectionIds())
                _ = _wsHandler.SendAsync(connId, MsgIds.GameOver, msg);
            _rooms.TryRemove(room.RoomId, out _);
        };

        _rooms[room.RoomId] = room;
        Console.WriteLine($"[RoomManager] Room created: {room.RoomId} ({playerIds.Length} players)");
        return room;
    }

    public GameRoom? GetRoom(string roomId) =>
        _rooms.TryGetValue(roomId, out var room) ? room : null;

    public bool DestroyRoom(string roomId) =>
        _rooms.TryRemove(roomId, out _);

    public void Tick(float dt)
    {
        foreach (var room in _rooms.Values)
            room.Tick(dt);
    }

    public IReadOnlyCollection<GameRoom> GetAllRooms() => _rooms.Values.ToList();
}
