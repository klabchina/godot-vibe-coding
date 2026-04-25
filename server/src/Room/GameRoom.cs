namespace Server.Room;

public sealed class GameRoom
{
    public string RoomId { get; }
    public IReadOnlyList<string> Players { get; }

    public GameRoom(string roomId, params string[] players)
    {
        RoomId = roomId;
        Players = players;
    }

    public void Tick(float dt)
    {
        _ = dt;
    }
}
