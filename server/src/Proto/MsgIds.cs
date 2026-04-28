namespace Server.Proto;

/// <summary>
/// 消息 ID 常量，与客户端共享
/// </summary>
public static class MsgIds
{
    // 匹配
    public const uint MatchRequest = 1001;
    public const uint MatchCancel = 1002;
    public const uint MatchSuccess = 1004;

    // 房间
    public const uint PlayerReady = 2001;
    public const uint GameStart = 2002;

    // 游戏
    public const uint PlayerMove = 3001;
    public const uint SkillChoice = 3002;
    public const uint GameEndSubmit = 3003;
    public const uint GameOver = 3005;
    public const uint LockstepFrame = 3008;

    // 系统
    public const uint Heartbeat = 9001;
}

public class Vec2
{
    public float X { get; set; }
    public float Y { get; set; }
}

// ---------- 匹配消息 ----------

public class MatchRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
}

public class MatchCancel
{
    public string PlayerId { get; set; } = string.Empty;
}

public class MatchSuccess
{
    public string RoomId { get; set; } = string.Empty;
    public List<PlayerInfo> Players { get; set; } = new();
}

public class PlayerInfo
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int Slot { get; set; }
}

// ---------- 房间消息 ----------

public class PlayerReady
{
    public string RoomId { get; set; } = string.Empty;
}

public class GameStartMsg
{
    public string RoomId { get; set; } = string.Empty;
    public int RandomSeed { get; set; }
}

// ---------- 游戏消息 ----------

public class PlayerMoveMsg
{
    public int Tick { get; set; }
    public Vec2 MoveDir { get; set; } = new();
}

public class SkillChoiceMsg
{
    public int Tick { get; set; }
    public string SkillId { get; set; } = string.Empty;
}

public class GameEndSubmitMsg
{
    public int Tick { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class PlayerFrameInput
{
    public string PlayerId { get; set; } = string.Empty;
    public int Slot { get; set; }
    public Vec2 MoveDir { get; set; } = new();
}

public class LockstepFrameMsg
{
    public int Frame { get; set; }
    public List<PlayerFrameInput> Inputs { get; set; } = new();
}

public class GameOverMsg
{
    public string RoomId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
