using System.Collections.Concurrent;
using Server.Room;

namespace Server.Match;

public sealed class MatchService
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly RoomManager _roomManager;

    public MatchService(RoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public void Enqueue(string playerId) => _queue.Enqueue(playerId);

    public void Tick()
    {
        while (_queue.Count >= 2)
        {
            _queue.TryDequeue(out var p1);
            _queue.TryDequeue(out var p2);

            if (!string.IsNullOrWhiteSpace(p1) && !string.IsNullOrWhiteSpace(p2))
            {
                _roomManager.CreateRoom(p1!, p2!);
            }
        }
    }
}
