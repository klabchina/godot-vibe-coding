using System.Collections.Generic;

namespace Game.Ecs.Components;

/// <summary>
/// Manages orbiting guard arrows around a player entity.
/// Each orbit arrow has its own angle and per-monster hit cooldown.
/// </summary>
public class OrbitComponent
{
    public int Count;                   // number of orbit arrows
    public float CurrentAngle;          // base rotation angle in degrees

    /// <summary>Per-orbit-arrow hit cooldowns: orbitIndex → (monsterId → remaining cooldown).</summary>
    public Dictionary<int, Dictionary<int, float>> HitCooldowns = new();
}
