using System.Collections.Concurrent;

namespace Server.Room;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public GameRoom CreateRoom(params string[] playerIds)
    {
        var room = new GameRoom(Guid.NewGuid().ToString("N"), playerIds);
        _rooms[room.RoomId] = room;
        Console.WriteLine($"Room created: {room.RoomId} with {playerIds.Length} players");
        return room;
    }

    public GameRoom? GetRoom(string roomId)
    {
        return _rooms.TryGetValue(roomId, out var room) ? room : null;
    }

    public bool DestroyRoom(string roomId)
    {
        return _rooms.TryRemove(roomId, out _);
    }

    public void Tick(float dt)
    {
        foreach (var room in _rooms.Values)
        {
            room.Tick(dt);
        }
    }

    public IReadOnlyCollection<GameRoom> GetAllRooms() => _rooms.Values.ToList();
}
