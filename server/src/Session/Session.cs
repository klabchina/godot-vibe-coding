namespace Server.Session;

public enum SessionState
{
    Idle,
    Matching,
    InRoom,
    InBattle
}

public sealed class Session
{
    public required string ConnectionId { get; init; }
    public required string PlayerId { get; init; }
    public string PlayerName { get; set; } = "";
    public SessionState State { get; set; } = SessionState.Idle;
    public string? RoomId { get; set; }
}
