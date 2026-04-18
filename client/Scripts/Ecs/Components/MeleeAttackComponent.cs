namespace Game.Ecs.Components;

/// <summary>
/// Marks a monster as capable of melee attacks.
/// Tracks cooldown and windup state.
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
}
