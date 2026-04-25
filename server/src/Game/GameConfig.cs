using Server.Proto;

namespace Server.Game;

/// <summary>
/// 游戏配置
/// </summary>
public static class GameConfig
{
    // 服务器设置
    public const int DefaultPort = 8080;
    public const int TickRate = 20;
    public const float TickInterval = 1f / TickRate;  // 50ms
    public const int MaxRooms = 100;
    
    // 匹配设置
    public const int MatchTimeoutSec = 60;
    public const int ReconnectTimeoutSec = 30;
    
    // 心跳设置
    public const int HeartbeatIntervalSec = 10;
    public const int HeartbeatTimeoutSec = 30;
    
    // 战斗设置
    public const float PlayerSpeed = 200f;
    public const int PlayerMaxHp = 100;
    public const int PlayerBaseDamage = 10;
    public const float PlayerAttackCooldown = 0.5f;
    
    // 箭矢设置
    public const float ArrowSpeed = 400f;
    public const float ArrowLifetime = 5f;
    
    // 怪物设置
    public const float SkeletonArrowSpeed = 280f;
    public const float EliteArrowSpeed = 150f;
    public const float EliteArrowAcceleration = 250f;
    
    // 地图设置
    public const float ArenaWidth = 1600f;
    public const float ArenaHeight = 900f;
    
    // 碰撞半径
    public const float PlayerCollisionRadius = 16f;
    public const float MonsterCollisionRadius = 20f;
    public const float ArrowCollisionRadius = 8f;
    
    // 波次配置
    public const int TotalWaves = 8;
    public const float WaveInterval = 5f;  // 波次间歇时间
    
    private static readonly Dictionary<MonsterType, MonsterConfig> _monsterConfigs = new()
    {
        [MonsterType.Slime] = new(30, 50, 5, 30),
        [MonsterType.Skeleton] = new(60, 80, 10, 40),
        [MonsterType.Orc] = new(120, 120, 20, 35),
        [MonsterType.Elite] = new(150, 100, 15, 50),
        [MonsterType.Boss] = new(500, 40, 35, 50),
    };
    
    public static MonsterConfig GetMonsterConfig(MonsterType type)
    {
        return _monsterConfigs.TryGetValue(type, out var config) 
            ? config 
            : new MonsterConfig(30, 50, 5, 30);
    }
}

public record MonsterConfig(int Hp, float Speed, int AttackDamage, float AttackRange);

/// <summary>
/// 升级配置
/// </summary>
public static class UpgradeConfig
{
    // 每级所需经验
    public static int XpForLevel(int level) => 100 + (level - 1) * 50;
    
    // 每级增长属性
    public static float DamageMultiplier(int level) => 1f + (level - 1) * 0.1f;
    public static float SpeedMultiplier(int level) => 1f + (level - 1) * 0.05f;
}
