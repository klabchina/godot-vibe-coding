using System;
using System.Collections.Generic;

namespace Game.Data;

public static class UpgradeData
{
    public record UpgradeDef(
        UpgradeId Id,
        string Name,
        UpgradeCategory Category,
        int MaxLevel
    );

    public static readonly Dictionary<UpgradeId, UpgradeDef> Definitions = new()
    {
        // Attack (6)
        [UpgradeId.MultiShot] = new(UpgradeId.MultiShot, "Multi Shot", UpgradeCategory.Attack, 7),
        [UpgradeId.AttackSpeed] = new(UpgradeId.AttackSpeed, "Attack Speed", UpgradeCategory.Attack, 5),
        [UpgradeId.DamageUp] = new(UpgradeId.DamageUp, "Damage Up", UpgradeCategory.Attack, 5),
        [UpgradeId.Pierce] = new(UpgradeId.Pierce, "Pierce", UpgradeCategory.Attack, 3),
        [UpgradeId.Bounce] = new(UpgradeId.Bounce, "Bounce Arrow", UpgradeCategory.Attack, 1),
        [UpgradeId.Explosion] = new(UpgradeId.Explosion, "Explosion", UpgradeCategory.Attack, 1),
        // Defense (4)
        [UpgradeId.MaxHpUp] = new(UpgradeId.MaxHpUp, "Max HP Up", UpgradeCategory.Defense, 99),
        [UpgradeId.MoveSpeedUp] = new(UpgradeId.MoveSpeedUp, "Move Speed Up", UpgradeCategory.Defense, 3),
        [UpgradeId.Shield] = new(UpgradeId.Shield, "Shield", UpgradeCategory.Defense, 1),
        [UpgradeId.Regen] = new(UpgradeId.Regen, "Regen", UpgradeCategory.Defense, 1),
        // Special (4)
        [UpgradeId.Magnet] = new(UpgradeId.Magnet, "Magnet", UpgradeCategory.Special, 99),
        [UpgradeId.FreezeArrow] = new(UpgradeId.FreezeArrow, "Freeze Arrow", UpgradeCategory.Special, 1),
        [UpgradeId.BurnArrow] = new(UpgradeId.BurnArrow, "Burn Arrow", UpgradeCategory.Special, 1),
        [UpgradeId.OrbitGuard] = new(UpgradeId.OrbitGuard, "Orbit Guard", UpgradeCategory.Special, 99),
    };

    // Category weights for random selection
    public const float AttackWeight = 0.50f;
    public const float DefenseWeight = 0.30f;
    public const float SpecialWeight = 0.20f;

    // Upgrade panel
    public const int ChoiceCount = 3;
    public const float ChoiceTimeoutSec = 5.0f;
    public const int GuaranteeAttackUntilLevel = 2; // First 2 upgrades guarantee at least 1 attack

    // --- Effect formulas ---

    // MultiShot: arrow count = 1 + level
    public static int GetArrowCount(int level) => 1 + level;

    // MultiShot: spread angle in degrees
    private static readonly float[] SpreadAngles = { 0f, 0f, 0f, 8f, 12f, 16f, 20f, 24f };
    public static float GetSpreadAngle(int level) => level >= 0 && level < SpreadAngles.Length ? SpreadAngles[level] : SpreadAngles[SpreadAngles.Length - 1];

    // AttackSpeed: cooldown = 0.80 * 0.85^level
    public static float GetCooldown(int level) => (float)(PlayerData.BaseCooldown * Math.Pow(0.85, level));

    // DamageUp: damage = 10 * (1 + 0.3 * level)
    public static int GetArrowDamage(int level) => (int)(PlayerData.BaseArrowDamage * (1 + 0.3f * level));

    // Pierce: count = level
    public static int GetPierceCount(int level) => level;

    // Bounce: search radius 120px, damage = 70% of original
    public const float BounceRadius = 120f;
    public const float BounceDamageRatio = 0.70f;

    // Explosion: radius 60px, damage = 50% of original
    public const float ExplosionRadius = 60f;
    public const float ExplosionDamageRatio = 0.50f;

    // MaxHpUp: maxHp = 100 * (1 + 0.2 * level), heal 20 per upgrade
    public static int GetMaxHp(int level) => (int)(PlayerData.BaseHp * (1 + 0.2f * level));
    public const int HpHealPerUpgrade = 20;

    // MoveSpeedUp: speed = 200 * (1 + 0.15 * level)
    public static float GetMoveSpeed(int level) => PlayerData.BaseMoveSpeed * (1 + 0.15f * level);

    // Shield: regenerate every 15 seconds, absorbs 1 hit
    public const float ShieldRegenInterval = 15f;

    // Regen: 1% MaxHP per second
    public const float RegenPercentPerSec = 0.01f;

    // Magnet: pickup radius = 50 * (1 + 0.5 * level)
    public static float GetPickupRadius(int level) => PlayerData.BasePickupRadius * (1 + 0.5f * level);

    // Freeze: 30% slow (Boss 15%), 2 seconds, no refresh
    public const float FreezeSlowPercent = 0.30f;
    public const float FreezeBossSlowPercent = 0.15f;
    public const float FreezeSlowDuration = 2.0f;

    // Burn: 3 dps for 3 seconds, refreshes on hit
    public const int BurnDotDamage = 3;
    public const float BurnDotDuration = 3.0f;

    // Orbit Guard: 180 deg/s, 80px radius, 8 damage, 0.5s hit interval
    public const float OrbitRotationSpeed = 180f; // degrees per second
    public const float OrbitRadius = 120f;
    public const int OrbitDamage = 10;
    public const float OrbitHitInterval = 0.25f;
}
