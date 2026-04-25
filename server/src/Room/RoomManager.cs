using System.Collections.Concurrent;

namespace Server.Room;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public GameRoom CreateRoom(string playerA, string playerB)
    {
        var room = new GameRoom(Guid.NewGuid().ToString("N"), playerA, playerB);
        _rooms[room.RoomId] = room;
        return room;
    }

    public void Tick(float dt)
    {
        foreach (var room in _rooms.Values)
        {
            room.Tick(dt);
        }
    }
}
