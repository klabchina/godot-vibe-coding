namespace Game.Ecs.Components;

/// <summary>
/// Tracks temporary buffs and persistent defensive upgrades on a player entity.
/// Only one timed buff (Frenzy or Invincible) active at a time — new overwrites old.
/// Shield and Regen are persistent once obtained via upgrade.
/// </summary>
public class BuffComponent
{
    // Timed buffs (mutually exclusive)
    public Data.BuffType? ActiveTimedBuff;
    public float TimedBuffRemaining;

    // Shield (from upgrade)
    public bool ShieldActive;       // currently has a shield charge
    public float ShieldCooldown;    // time until next shield regens (counts down from 15)

    // Regen (from upgrade)
    public bool RegenActive;
    public float RegenAccumulator;  // fractional HP accumulator
}
