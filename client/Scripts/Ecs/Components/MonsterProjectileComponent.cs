namespace Game.Ecs.Components;

/// <summary>
/// Marks an entity as a monster-fired projectile.
/// Acceleration = 0 → constant speed (Skeleton).
/// Acceleration > 0 → linearly accelerating (Elite).
/// </summary>
public class MonsterProjectileComponent
{
    public int   Damage;
    public int   OwnerId;      // Entity ID of the monster that fired this
    public float Acceleration; // px/s² added to speed each frame; 0 = constant
}
