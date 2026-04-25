using Server.Proto;

namespace Server.Game;

/// <summary>
/// 怪物动作状态
/// </summary>
// 使用 Server.Proto.MonsterStateType

/// <summary>
/// 怪物状态（服务器权威）
/// </summary>
public sealed class MonsterState
{
    private static int _nextId = 1;
    
    public int Id { get; init; }
    public MonsterType Type { get; init; }
    
    // 位置与移动
    public float X { get; set; }
    public float Y { get; set; }
    public float VX { get; set; }
    public float VY { get; set; }
    public float Rotation { get; set; }
    
    // 战斗属性
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int AttackDamage { get; set; }
    public float AttackRange { get; set; }
    public float Speed { get; set; }
    
    // 状态
    public MonsterStateType State { get; set; } = MonsterStateType.Walk;
    public MonsterStateType LastState { get; set; } = MonsterStateType.Walk;
    
    // AI 状态
    public float StateTimer { get; set; }
    public float AttackCooldown { get; set; }
    public string? TargetPlayerId { get; set; }
    public bool FiredThisCycle { get; set; }
    
    // 怪物特有属性
    public float WanderTimer { get; set; }
    public float WanderDirX { get; set; } = 1;
    public float WanderDirY { get; set; }
    
    public bool IsAlive => Hp > 0;
    public bool IsDead => State == MonsterStateType.Death;
    
    public Vec2 Position => new() { X = X, Y = Y };
    public Vec2 Velocity => new() { X = VX, Y = VY };
    
    public MonsterState(MonsterType type, float x, float y)
    {
        Id = Interlocked.Increment(ref _nextId);
        Type = type;
        X = x;
        Y = y;
        
        // 根据类型设置属性
        var config = GameConfig.GetMonsterConfig(type);
        MaxHp = config.Hp;
        Hp = config.Hp;
        Speed = config.Speed;
        AttackDamage = config.AttackDamage;
        AttackRange = config.AttackRange;
    }
    
    public MonsterStateMsg ToMsg()
    {
        return new MonsterStateMsg
        {
            MonsterId = Id,
            MonsterType = (int)Type,
            Position = Position,
            Velocity = Velocity,
            Rotation = Rotation,
            Hp = Hp,
            MaxHp = MaxHp,
            State = (int)State
        };
    }
    
    /// <summary>
    /// 标记为死亡状态
    /// </summary>
    public void MarkDead()
    {
        State = MonsterStateType.Death;
    }
    
    /// <summary>
    /// 造成伤害
    /// </summary>
    public void TakeDamage(int damage, string killerId)
    {
        Hp -= damage;
        if (Hp <= 0)
        {
            Hp = 0;
            MarkDead();
        }
    }
    
    /// <summary>
    /// 重置 ID 计数器（仅用于测试）
    /// </summary>
    public static void ResetIdCounter() => _nextId = 0;
}
