using System;
using System.Collections.Generic;
using Game.Ecs.Components;

namespace Game.Data;

public static class MonsterData
{
    public record MonsterBase(
        int Hp, float Speed, float Damage, int Radius, int BaseXp, int FirstWave,
        ColliderShape Shape = ColliderShape.Circle, int HalfWidth = 0, int HalfHeight = 0);

    public static readonly Dictionary<MonsterType, MonsterBase> Base = new()
    {
        [MonsterType.Slime] = new(Hp: 20, Speed: 60, Damage: 5, Radius: 20, BaseXp: 5, FirstWave: 1),
        [MonsterType.Skeleton] = new(Hp: 40, Speed: 90, Damage: 8, Radius: 20, BaseXp: 8, FirstWave: 2, Shape: ColliderShape.Box, HalfWidth: 20, HalfHeight: 40),
        [MonsterType.Orc] = new(Hp: 80, Speed: 50, Damage: 15, Radius: 25, BaseXp: 15, FirstWave: 3),
        [MonsterType.Elite] = new(Hp: 150, Speed: 70, Damage: 12, Radius: 25, BaseXp: 30, FirstWave: 4, Shape: ColliderShape.Box, HalfWidth: 25, HalfHeight: 40),
        [MonsterType.Boss] = new(Hp: 1000, Speed: 40, Damage: 25, Radius: 40, BaseXp: 100, FirstWave: 8),
    };

    private const float HpGrowthRate = 0.10f;
    private const float DamageGrowthRate = 0.08f;
    private const float XpGrowthRate = 0.10f;

    public static int GetHp(MonsterType type, int wave) => (int)(Base[type].Hp * (1 + HpGrowthRate * Math.Max(0, wave - Base[type].FirstWave)));
    public static float GetDamage(MonsterType type, int wave) => Base[type].Damage * (1 + DamageGrowthRate * Math.Max(0, wave - Base[type].FirstWave));
    public static int GetXp(MonsterType type, int wave) => (int)Math.Ceiling(Base[type].BaseXp * (1 + XpGrowthRate * Math.Max(0, wave - Base[type].FirstWave)));
    public static float GetSpeed(MonsterType type) => Base[type].Speed;
    public static int GetRadius(MonsterType type) => Base[type].Radius;
    public static ColliderShape GetShape(MonsterType type) => Base[type].Shape;
    public static int GetHalfWidth(MonsterType type) => Base[type].HalfWidth;
    public static int GetHalfHeight(MonsterType type) => Base[type].HalfHeight;

    // Boss phase data (fixed, no growth)
    public record BossPhaseData(float HpThresholdPercent, float Speed, float Damage, int Radius);

    public static readonly Dictionary<BossPhase, BossPhaseData> BossPhases = new()
    {
        [BossPhase.Chase] = new(HpThresholdPercent: 1.0f, Speed: 40, Damage: 25, Radius: 40),
        [BossPhase.Summon] = new(HpThresholdPercent: 2f / 3f, Speed: 0, Damage: 0, Radius: 40),
        [BossPhase.Frenzy] = new(HpThresholdPercent: 1f / 3f, Speed: 100, Damage: 40, Radius: 30),
    };

    public const int BossSummonCount = 3;
    public const float BossSummonCooldown = 2.0f;
    public const float BossSummonDuration = 8.0f;
    public const int BossPhaseChangeXp = 30;

    // Orc dash parameters
    public const float OrcDashSpeed = 300f;      // 5x walk speed (walk = 50)
    public const float OrcDashDurationMin = 0.8f;  // dash duration random min
    public const float OrcDashDurationMax = 2.0f;  // dash duration random max
    public const float OrcDashIntervalMin = 2.0f; // min interval between dashes
    public const float OrcDashIntervalMax = 7.0f; // max interval between dashes

    // Boss dash parameters
    public const float BossDashSpeed = 500f;      // faster than Orc
    public const float BossDashDurationMin = 1.0f;
    public const float BossDashDurationMax = 2.5f;
    public const float BossDashIntervalMin = 1.5f;
    public const float BossDashIntervalMax = 4.0f;

    // Melee attack parameters (shared)
    public const float MeleeAttackRange = 100f;       // px, attack triggers within this distance
    public const float MeleeWindupDuration = 0.1f;  // seconds before damage frames

    // Hitbox config per monster type (used during damage frame)
    public record HitboxData(float Radius, ColliderShape Shape);
    public static readonly Dictionary<MonsterType, HitboxData> Hitbox = new()
    {
        [MonsterType.Slime]  = new(Radius: 25f, Shape: ColliderShape.Circle),
        [MonsterType.Orc]    = new(Radius: 35f, Shape: ColliderShape.Circle),
        [MonsterType.Boss]   = new(Radius: 55f, Shape: ColliderShape.Circle),
    };

    // Slime attack parameters
    public const float SlimeAttackDamage = 8f;
    public const float SlimeAttackCooldownMin = 0.2f;
    public const float SlimeAttackCooldownMax = 2.0f;

    // Orc attack parameters
    public const float OrcAttackDamage = 20f;
    public const float OrcAttackCooldownMin = 0.5f;
    public const float OrcAttackCooldownMax = 1.0f;

    // Boss attack parameters
    public const float BossAttackDamage = 30f;
    public const float BossAttackCooldownMin = 0.25f;
    public const float BossAttackCooldownMax = 0.5f;

    // Skeleton ranged parameters
    public const float SkeletonWanderDuration = 2.0f;  // seconds in wander phase
    public const float SkeletonAttackDuration = 0.4f;  // pause before firing
    public const float SkeletonProjectileSpeed = 280f;  // px/s (constant)
    public const int SkeletonProjectileDamage = 6;

    // Elite ranged parameters
    public const float EliteWanderDuration = 2.5f;  // seconds in wander phase
    public const float EliteAttackDuration = 0.6f;  // pause before firing
    public const float EliteProjectileSpeed = 120f;  // px/s 慢速追踪
    public const float EliteProjectileTurnSpeed = 100f;  // °/s 每秒最大转向角度
    public const float EliteProjectileLifetime = 10f;   // 秒，未命中存活时间
    public const int EliteProjectileMinCount = 2;
    public const int EliteProjectileMaxCount = 4;
    public const float EliteProjectileSpreadDeg = 12f;   // degrees between adjacent shots
    public const int EliteProjectileDamage = 10;
}
