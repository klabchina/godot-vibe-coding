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
