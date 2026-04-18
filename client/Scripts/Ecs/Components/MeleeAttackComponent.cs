namespace Game.Ecs.Components;

/// <summary>
/// Marks a monster as capable of melee attacks.
/// Tracks cooldown, windup state, and the current attack target.
/// </summary>
public class MeleeAttackComponent
{
    /// <summary>
    /// Seconds until next attack is ready. Set after damage frame.
    /// </summary>
    public float CooldownTimer;

    /// <summary>
    /// Seconds remaining in windup (attack animation plays during this).
    /// 0 means not in windup / not attacking.
    /// </summary>
    public float AttackWindupTimer;

    /// <summary>
    /// Whether the monster can start a new attack.
    /// False while in cooldown.
    /// </summary>
    public bool CanAttack = true;

    /// <summary>
    /// Entity ID of the player targeted for the current attack.
    /// Set when windup starts; used to place the hitbox in front of the monster.
    /// </summary>
    public int TargetId;
}
