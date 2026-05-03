using Server.Proto;

namespace Server.Session;

/// <summary>
/// 会话状态
/// </summary>
public enum SessionState
{
    Idle,       // 空闲，在主界面
    Matching,   // 匹配中
    InRoom,     // 已进入房间，等待开始
    InBattle,   // 战斗中
}

/// <summary>
/// 玩家会话
/// </summary>
public sealed class Session
{
    public string ConnectionId { get; init; } = "";
    public string PlayerId { get; init; } = "";
    public string PlayerName { get; set; } = "";
    public SessionState State { get; set; } = SessionState.Idle;
    public string? RoomId { get; set; }
    
    // 心跳
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
}
