using Server.Proto;

namespace Server.Game;

/// <summary>
/// 玩家动作状态
/// </summary>
// 使用 Server.Proto.PlayerAction

/// <summary>
/// 玩家状态（服务器权威）
/// </summary>
public sealed class PlayerState
{
    public required string PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public string OwnerId { get; init; } = "";
    public int Slot { get; set; }  // 0 或 1
    
    // 位置与移动
    public float X { get; set; }
    public float Y { get; set; }
    public float AimAngle { get; set; }
    public float MoveDirX { get; set; }
    public float MoveDirY { get; set; }
    
    // 战斗属性
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int XpToNextLevel { get; set; } = 100;
    
    // 统计数据
    public int KillCount { get; set; }
    public int TotalDamage { get; set; }
    public int ArrowsFired { get; set; }
    
    // 状态
    public PlayerAction Action { get; set; } = PlayerAction.Idle;
    public bool IsAlive => Hp > 0;
    public bool IsCharging { get; set; }
    public float ChargePower { get; set; }
    
    // 输入缓冲
    public PlayerInputMsg? LastInput { get; set; }
    
    public Vec2 Position => new() { X = X, Y = Y };
    
    public PlayerStateMsg ToMsg()
    {
        return new PlayerStateMsg
        {
            PlayerId = PlayerId,
            Position = Position,
            AimAngle = AimAngle,
            Hp = Hp,
            MaxHp = MaxHp,
            Action = (int)Action,
            Level = Level,
            Xp = Xp
        };
    }
}
