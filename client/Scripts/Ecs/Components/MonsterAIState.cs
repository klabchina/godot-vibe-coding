using Game.Ecs.Core;

namespace Game.Ecs.Components;

/// <summary>
/// Ranged phase for Skeleton and Elite: wander briefly then stop and shoot.
/// </summary>
public enum RangedPhase { Wander, Attack }

/// <summary>
/// Per-monster AI state for advanced behaviors.
/// - Skeleton/Elite: ranged wander-attack cycle
/// - Orc: timed dash toward player
/// </summary>
public class MonsterAIState
{
    // 当前锁定的目标玩家 ID（-1 = 无目标），用于 RenderSystem 翻转判定
    public int TargetId = -1;

    // Orc dash (accelerating charge toward player)
    public bool  IsDashing;
    public float DashTimer;       // elapsed dash time (counts up 0 → DashDuration)
    public float DashInterval;    // time between dashes (2-7s random)

    // Ranged (Skeleton & Elite)
    public RangedPhase RangedPhase = RangedPhase.Wander;
    public float       PhaseTimer;        // counts down; 0 triggers phase transition/re-init
    public Vec2        WanderDir;         // direction chosen at wander-phase start
    public bool        FiredThisCycle;    // ensures one fire per attack phase

    // Detour memory — commit to a bypass direction until path clears
    public bool  IsDetouring;
    public Vec2  DetourDir;
    public float DetourTimer;
}
