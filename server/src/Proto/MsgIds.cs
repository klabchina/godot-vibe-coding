namespace Server.Proto;

/// <summary>
/// 消息 ID 常量，与客户端共享
/// </summary>
public static class MsgIds
{
    // ========== 匹配相关 (1001-1999) ==========
    /// <summary>Client → Server: 请求匹配</summary>
    public const uint MatchRequest = 1001;
    
    /// <summary>Client → Server: 取消匹配</summary>
    public const uint MatchCancel = 1002;
    
    /// <summary>Server → Client: 匹配状态更新</summary>
    public const uint MatchUpdate = 1003;
    
    /// <summary>Server → Client: 匹配成功</summary>
    public const uint MatchSuccess = 1004;

    // ========== 房间相关 (2001-2999) ==========
    /// <summary>Client → Server: 玩家准备</summary>
    public const uint PlayerReady = 2001;
    
    /// <summary>Server → Client: 战斗开始</summary>
    public const uint BattleStart = 2002;

    // ========== 战斗相关 (3001-3999) ==========
    /// <summary>Client → Server: 玩家输入</summary>
    public const uint PlayerInput = 3001;
    
    /// <summary>Server → Client: 游戏状态快照</summary>
    public const uint GameStateSnapshot = 3002;
    
    /// <summary>Server → Client: 实体死亡</summary>
    public const uint EntityDeath = 3003;
    
    /// <summary>Server → Client: 波次开始</summary>
    public const uint WaveStart = 3004;
    
    /// <summary>Server → Client: 游戏结束</summary>
    public const uint GameOver = 3005;
    
    /// <summary>Server → Client: 升级事件</summary>
    public const uint LevelUp = 3006;
    
    /// <summary>Server → Client: 玩家数据更新</summary>
    public const uint PlayerUpdate = 3007;

    // ========== 系统相关 (9001-9999) ==========
    /// <summary>Client ↔ Server: 心跳</summary>
    public const uint Heartbeat = 9001;
    
    /// <summary>Server → Client: 断开连接</summary>
    public const uint Disconnect = 9002;
}

// ============================================
// Protobuf Message C# 实现（客户端/服务端共享）
// ============================================

/// <summary>
/// 向量2
/// </summary>
public class Vec2
{
    public float X { get; set; }
    public float Y { get; set; }
}

/// <summary>
/// 消息信封
/// </summary>
public class Envelope
{
    public uint MsgId { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public ulong Timestamp { get; set; }
}

// ---------- 匹配消息 ----------

public class MatchRequest
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
}

public class MatchCancel
{
    public string PlayerId { get; set; } = "";
}

public class MatchUpdate
{
    public MatchStatus Status { get; set; }
    public float WaitTime { get; set; }
    
    public enum MatchStatus
    {
        Waiting = 0,
        Found = 1,
        Cancelled = 2,
        Timeout = 3
    }
}

public class MatchSuccess
{
    public string RoomId { get; set; } = "";
    public List<PlayerInfo> Players { get; set; } = new();
}

public class PlayerInfo
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int Slot { get; set; }
}

// ---------- 房间消息 ----------

public class PlayerReady
{
    public string RoomId { get; set; } = "";
}

public class BattleStart
{
    public string RoomId { get; set; } = "";
    public int RandomSeed { get; set; }
    public List<WaveConfigMsg> Waves { get; set; } = new();
}

public class WaveConfigMsg
{
    public int WaveNumber { get; set; }
    public List<SpawnEntryMsg> Spawns { get; set; } = new();
    public float SpawnInterval { get; set; }
    public float PreWaveDelay { get; set; }
}

public class SpawnEntryMsg
{
    public int MonsterType { get; set; }
    public int Count { get; set; }
    public Vec2 Position { get; set; } = new();
}

// ---------- 战斗消息 ----------

public class PlayerInputMsg
{
    public int Tick { get; set; }
    public Vec2 MoveDir { get; set; } = new();
    public float AimAngle { get; set; }
    public bool Shoot { get; set; }
    public float ChargePower { get; set; }
}

public class GameStateSnapshotMsg
{
    public int ServerTick { get; set; }
    public List<PlayerStateMsg> Players { get; set; } = new();
    public List<ArrowStateMsg> Arrows { get; set; } = new();
    public List<MonsterStateMsg> Monsters { get; set; } = new();
    public WaveInfoMsg? WaveInfo { get; set; }
}

public class PlayerStateMsg
{
    public string PlayerId { get; set; } = "";
    public Vec2 Position { get; set; } = new();
    public float AimAngle { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Action { get; set; }
    public int Level { get; set; }
    public int Xp { get; set; }
}

public class ArrowStateMsg
{
    public int ArrowId { get; set; }
    public string OwnerId { get; set; } = "";
    public Vec2 Position { get; set; } = new();
    public Vec2 Velocity { get; set; } = new();
    public float Rotation { get; set; }
    public int Damage { get; set; }
    public bool IsPlayerArrow { get; set; }
}

public class MonsterStateMsg
{
    public int MonsterId { get; set; }
    public int MonsterType { get; set; }
    public Vec2 Position { get; set; } = new();
    public Vec2 Velocity { get; set; } = new();
    public float Rotation { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int State { get; set; }
}

public class WaveInfoMsg
{
    public int CurrentWave { get; set; }
    public int TotalWaves { get; set; }
    public int MonstersRemaining { get; set; }
    public float IntervalCountdown { get; set; }
}

// ---------- 事件消息 ----------

public class EntityDeathMsg
{
    public EntityType Type { get; set; }
    public int EntityId { get; set; }
    public Vec2 Position { get; set; } = new();
    public int KillerId { get; set; }
    
    public enum EntityType
    {
        Monster = 0,
        Arrow = 1,
        Player = 2
    }
}

public class WaveStartMsg
{
    public int WaveNumber { get; set; }
    public int MonsterCount { get; set; }
}

public class GameOverMsg
{
    public GameResult Result { get; set; }
    public int WavesCleared { get; set; }
    public int TotalKills { get; set; }
    public List<PlayerScoreMsg> Scores { get; set; } = new();
    
    public enum GameResult
    {
        Victory = 0,
        Defeat = 1,
        Disconnect = 2
    }
}

public class PlayerScoreMsg
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int Kills { get; set; }
    public int DamageDealt { get; set; }
    public int ArrowsFired { get; set; }
    public int Level { get; set; }
}

public class LevelUpMsg
{
    public string PlayerId { get; set; } = "";
    public int NewLevel { get; set; }
    public int UpgradePoints { get; set; }
}

public class PlayerUpdateMsg
{
    public string PlayerId { get; set; } = "";
    public int HpDelta { get; set; }
    public int XpDelta { get; set; }
    public int Level { get; set; }
    public int KillCount { get; set; }
    public int DamageDealt { get; set; }
}

/// <summary>
/// 怪物类型枚举
/// </summary>
public enum MonsterType
{
    Slime = 1,
    Skeleton = 2,
    Orc = 3,
    Elite = 4,
    Boss = 5
}

/// <summary>
/// 玩家动作状态
/// </summary>
public enum PlayerAction
{
    Idle = 0,
    Moving = 1,
    Charging = 2,
    Shooting = 3
}

/// <summary>
/// 怪物动作状态
/// </summary>
public enum MonsterStateType
{
    Walk = 0,
    Attack = 1,
    Death = 2
}
