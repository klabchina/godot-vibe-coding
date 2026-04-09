namespace Game.Ecs.Components;

/// <summary>
/// Per-monster AI state for advanced behaviors (Skeleton dodge, Orc charge).
/// </summary>
public class MonsterAIState
{
    // Skeleton dodge
    public float DodgeTimer;         // counts up to DodgeInterval (3s)
    public float DodgeDuration;      // remaining dodge move time (0.5s)
    public Godot.Vector2 DodgeDir;   // random lateral direction

    // Orc charge
    public bool IsCharging;
    public bool IsStunned;
    public float StunTimer;
}
