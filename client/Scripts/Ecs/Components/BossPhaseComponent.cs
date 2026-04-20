using Game.Data;

namespace Game.Ecs.Components;

/// <summary>
/// Tracks Boss AI phase state. Attached to Boss monster entities.
/// </summary>
public class BossPhaseComponent
{
    public BossPhase Phase = BossPhase.Chase;
    public float SummonTimer;       // countdown to next summon batch
    public float SummonDuration;    // remaining time in summon phase (10s)
    public bool Phase2Triggered;
    public bool Phase3Triggered;

    // Dash state (shared across all phases)
    public bool IsDashing;
    public float DashTimer;         // time within current dash/interval
    public float DashInterval;      // reused: countdown to dash, then dash duration
}
