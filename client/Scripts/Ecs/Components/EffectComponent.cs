using System.Collections.Generic;

namespace Game.Ecs.Components;

/// <summary>
/// Tracks active status effects (freeze slow, burn DoT) on a monster entity.
/// </summary>
public class EffectComponent
{
    // Freeze
    public bool IsFrozen;
    public float FreezeTimer;       // remaining slow duration
    public float FreezeSlowPercent; // 0.30 normal, 0.15 boss

    // Burn
    public bool IsBurning;
    public float BurnTimer;         // remaining burn duration
    public float BurnTickTimer;     // timer for next tick
    public int BurnDamagePerTick;   // damage per second

    // Track which arrows already hit (for pierce dedup in effects)
    public HashSet<int> HitByArrows = new();
}
