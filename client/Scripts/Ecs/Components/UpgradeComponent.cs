namespace Game.Ecs.Components;

/// <summary>
/// Tracks all upgrade levels for a player entity. Attached alongside PlayerComponent.
/// </summary>
public class UpgradeComponent
{
    // Attack (6)
    public int MultiShotLevel;      // 0-7, arrow count = 1 + level
    public int AttackSpeedLevel;    // 0-5, cooldown = 0.80 * 0.85^level
    public int DamageLevel;         // 0-5, damage = 10 * (1 + 0.3 * level)
    public int PierceLevel;         // 0-3, pierce count = level
    public bool HasBounce;          // one-time
    public bool HasExplosion;       // one-time

    // Defense (4)
    public int MaxHpLevel;          // no cap, maxHp = 100 * (1 + 0.2 * level)
    public int MoveSpeedLevel;      // 0-3, speed = 200 * (1 + 0.15 * level)
    public bool HasShield;          // one-time
    public bool HasRegen;           // one-time

    // Special (4)
    public int MagnetLevel;         // no cap, pickup radius = 50 * (1 + 0.5 * level)
    public bool HasFreeze;          // one-time
    public bool HasBurn;            // one-time
    public int OrbitCount;          // no cap, +1 orbit per level

    /// <summary>Get current level for a given upgrade ID.</summary>
    public int GetLevel(Data.UpgradeId id) => id switch
    {
        Data.UpgradeId.MultiShot   => MultiShotLevel,
        Data.UpgradeId.AttackSpeed => AttackSpeedLevel,
        Data.UpgradeId.DamageUp    => DamageLevel,
        Data.UpgradeId.Pierce      => PierceLevel,
        Data.UpgradeId.Bounce      => HasBounce ? 1 : 0,
        Data.UpgradeId.Explosion   => HasExplosion ? 1 : 0,
        Data.UpgradeId.MaxHpUp     => MaxHpLevel,
        Data.UpgradeId.MoveSpeedUp => MoveSpeedLevel,
        Data.UpgradeId.Shield      => HasShield ? 1 : 0,
        Data.UpgradeId.Regen       => HasRegen ? 1 : 0,
        Data.UpgradeId.Magnet      => MagnetLevel,
        Data.UpgradeId.FreezeArrow => HasFreeze ? 1 : 0,
        Data.UpgradeId.BurnArrow   => HasBurn ? 1 : 0,
        Data.UpgradeId.OrbitGuard  => OrbitCount,
        _ => 0,
    };

    /// <summary>Apply one level of the given upgrade.</summary>
    public void Apply(Data.UpgradeId id)
    {
        switch (id)
        {
            case Data.UpgradeId.MultiShot:   MultiShotLevel++;   break;
            case Data.UpgradeId.AttackSpeed: AttackSpeedLevel++; break;
            case Data.UpgradeId.DamageUp:    DamageLevel++;      break;
            case Data.UpgradeId.Pierce:      PierceLevel++;      break;
            case Data.UpgradeId.Bounce:      HasBounce = true;   break;
            case Data.UpgradeId.Explosion:   HasExplosion = true; break;
            case Data.UpgradeId.MaxHpUp:     MaxHpLevel++;       break;
            case Data.UpgradeId.MoveSpeedUp: MoveSpeedLevel++;   break;
            case Data.UpgradeId.Shield:      HasShield = true;   break;
            case Data.UpgradeId.Regen:       HasRegen = true;    break;
            case Data.UpgradeId.Magnet:      MagnetLevel++;      break;
            case Data.UpgradeId.FreezeArrow: HasFreeze = true;   break;
            case Data.UpgradeId.BurnArrow:   HasBurn = true;     break;
            case Data.UpgradeId.OrbitGuard:  OrbitCount++;        break;
        }
    }
}
