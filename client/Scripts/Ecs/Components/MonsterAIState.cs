using Game.Ecs.Core;

namespace Game.Ecs.Components;

/// <summary>
/// Ranged phase for Skeleton and Elite: wander briefly then stop and shoot.
/// </summary>
public enum RangedPhase { Wander, Attack }

/// <summary>
/// Per-monster AI state for advanced behaviors.
/// - Skeleton/Elite: ranged wander-attack cycle
/// - Orc: charge + stun
/// </summary>
public class MonsterAIState
{
    // Orc charge
    public bool  IsCharging;
    public bool  IsStunned;
    public float StunTimer;

    // Ranged (Skeleton & Elite)
    public RangedPhase RangedPhase = RangedPhase.Wander;
    public float       PhaseTimer;        // counts down; 0 triggers phase transition/re-init
    public Vec2        WanderDir;         // direction chosen at wander-phase start
    public bool        FiredThisCycle;    // ensures one fire per attack phase
}
