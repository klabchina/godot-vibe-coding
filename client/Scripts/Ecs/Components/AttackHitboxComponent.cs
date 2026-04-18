namespace Game.Ecs.Components;

/// <summary>
/// Marks an entity as an active attack hitbox (created transiently during a monster's
/// damage frame). The hitbox checks collision against Player entities and deals damage
/// once, then is destroyed automatically by the system that creates it.
/// </summary>
public class AttackHitboxComponent
{
    /// <summary>The entity ID of the monster that created this hitbox.</summary>
    public int AttackerId;

    /// <summary>Damage to deal to the player on contact.</summary>
    public int Damage;
}