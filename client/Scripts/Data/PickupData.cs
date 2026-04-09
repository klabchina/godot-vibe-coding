namespace Game.Data;

public static class PickupData
{
    // Drop chance (total 5%)
    public const float TotalDropChance     = 0.05f;
    public const float HealthPotionChance  = 0.02f;
    public const float FrenzyChance        = 0.015f;
    public const float InvincibleChance    = 0.005f;
    public const float BombChance          = 0.01f;

    // Effect values
    public const float HealthPotionHealPercent = 0.25f;
    public const float FrenzyDuration          = 5.0f;
    public const float FrenzyShootMultiplier   = 2.0f; // cooldown halved
    public const float InvincibleDuration      = 3.0f;
    public const int   BombDamage              = 50;

    // Exp orb physics
    public const float ExpOrbPickupRadius = 50f;  // affected by magnet upgrade
    public const float ExpOrbFlySpeed     = 300f;
    public const float ExpOrbLifeTime     = 30f;
    public const float ExpOrbBlinkTime    = 5.0f; // last 5 seconds blink

    // Boss always drops 1 random item
    public const bool BossGuaranteedDrop = true;
}
