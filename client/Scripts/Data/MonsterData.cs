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
        [MonsterType.Boss] = new(Hp: 500, Speed: 40, Damage: 25, Radius: 40, BaseXp: 100, FirstWave: 8),
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
        [BossPhase.Frenzy] = new(HpThresholdPercent: 1f / 3f, Speed: 80, Damage: 30, Radius: 30),
    };

    public const int BossSummonCount = 3;
    public const float BossSummonCooldown = 3.0f;
    public const float BossSummonDuration = 10.0f;
    public const int BossPhaseChangeXp = 30;

    // Orc charge parameters
    public const float OrcChargeRange = 150f;
    public const float OrcChargeSpeed = 150f;
    public const float OrcStunDuration = 1.0f;

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
