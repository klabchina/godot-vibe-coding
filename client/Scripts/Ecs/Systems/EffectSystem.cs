using System.Collections.Generic;
using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

/// <summary>
/// Processes arrow on-hit effects: bounce, explosion, freeze slow, burn DoT.
/// Runs after DamageSystem, reads CollisionSystem.Hits.
/// </summary>
public class EffectSystem : GameSystem
{
    public override void Update(float delta)
    {
        ProcessArrowHitEffects();
        TickBurnDamage(delta);
        TickFreezeDuration(delta);
    }

    private void ProcessArrowHitEffects()
    {
        var collisionSystem = World.GetSystem<CollisionSystem>();
        if (collisionSystem == null) return;

        foreach (var hit in collisionSystem.Hits)
        {
            if (!hit.IsArrow) continue;

            var arrowEntity = World.GetEntity(hit.AttackerId);
            if (arrowEntity == null) continue;

            var arrow = arrowEntity.Get<ArrowComponent>();
            if (arrow == null) continue;

            var defender = World.GetEntity(hit.DefenderId);
            if (defender == null || !defender.IsAlive) continue;

            // Ensure EffectComponent exists on target
            var effect = defender.Get<EffectComponent>();
            if (effect == null)
            {
                effect = new EffectComponent();
                defender.Add(effect);
            }

            // --- Freeze ---
            if (arrow.Freezing && !effect.IsFrozen)
            {
                var monster = defender.Get<MonsterComponent>();
                bool isBoss = monster != null && monster.Type == MonsterType.Boss;

                effect.IsFrozen = true;
                effect.FreezeTimer = UpgradeData.FreezeSlowDuration;
                effect.FreezeSlowPercent = isBoss
                    ? UpgradeData.FreezeBossSlowPercent
                    : UpgradeData.FreezeSlowPercent;
            }

            // --- Burn ---
            if (arrow.Burning)
            {
                // Refresh burn duration on every hit (no stacking, but refreshes timer)
                effect.IsBurning = true;
                effect.BurnTimer = UpgradeData.BurnDotDuration;
                effect.BurnDamagePerTick = UpgradeData.BurnDotDamage;
                // Don't reset tick timer if already burning (continue from current tick)
            }

            // --- Explosion (AOE) ---
            if (arrow.Explosive)
            {
                ApplyExplosion(defender, arrow);
            }

            // --- Bounce ---
            if (arrow.Bouncing && !arrowEntity.Has<BounceMarkerComponent>())
            {
                SpawnBounceArrow(arrowEntity, defender, arrow);
            }
        }
    }

    private void ApplyExplosion(Entity target, ArrowComponent arrow)
    {
        var targetTransform = target.Get<TransformComponent>();
        if (targetTransform == null) return;

        int aoeDamage = (int)(arrow.Damage * UpgradeData.ExplosionDamageRatio);
        float radius = UpgradeData.ExplosionRadius;

        var monsters = World.GetEntitiesWith<MonsterComponent, TransformComponent, HealthComponent>();
        foreach (var monster in monsters)
        {
            if (!monster.IsAlive || monster.Id == target.Id) continue;

            var monsterTransform = monster.Get<TransformComponent>();
            float dist = targetTransform.Position.DistanceTo(monsterTransform.Position);
            if (dist <= radius)
            {
                if (monster.Has<DeathPendingComponent>()) continue;
                var health = monster.Get<HealthComponent>();
                health.Hp -= aoeDamage;
                if (health.Hp < 0) health.Hp = 0;

                // Track damage to player stats
                var owner = World.GetEntity(arrow.OwnerId);
                var playerComp = owner?.Get<PlayerComponent>();
                if (playerComp != null)
                    playerComp.TotalDamageDealt += aoeDamage;
            }
        }
    }

    private void SpawnBounceArrow(Entity originalArrow, Entity hitTarget, ArrowComponent originalArrowComp)
    {
        var hitPos = hitTarget.Get<TransformComponent>()?.Position ?? Vec2.Zero;

        // Find nearest un-hit monster within bounce radius
        Entity bounceTarget = null;
        float nearestDist = UpgradeData.BounceRadius;

        var monsters = World.GetEntitiesWith<MonsterComponent, TransformComponent, HealthComponent>();
        foreach (var monster in monsters)
        {
            if (!monster.IsAlive || monster.Id == hitTarget.Id) continue;
            if (monster.Has<DeathPendingComponent>()) continue;
            var monsterTransform = monster.Get<TransformComponent>();
            float dist = hitPos.DistanceTo(monsterTransform.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                bounceTarget = monster;
            }
        }

        if (bounceTarget == null) return;

        var targetPos = bounceTarget.Get<TransformComponent>().Position;
        Vec2 dir = (targetPos - hitPos).Normalized();
        int bounceDamage = (int)(originalArrowComp.Damage * UpgradeData.BounceDamageRatio);

        var arrow = World.CreateEntity();
        arrow.Add(new TransformComponent { Position = hitPos, Rotation = dir.Angle() });
        arrow.Add(new VelocityComponent { Velocity = dir * PlayerData.ArrowSpeed, Speed = PlayerData.ArrowSpeed });
        arrow.Add(new ArrowComponent
        {
            Damage = bounceDamage,
            OwnerId = originalArrowComp.OwnerId,
            PierceCount = 0,
            Bouncing = false, // bounced arrow doesn't bounce again
            Explosive = originalArrowComp.Explosive,
            Freezing = originalArrowComp.Freezing,
            Burning = originalArrowComp.Burning
        });
        arrow.Add(new BounceMarkerComponent()); // prevent infinite bounce
        arrow.Add(new ColliderComponent
        {
            Radius = 5f,
            Layer = CollisionLayers.Arrow,
            Mask = CollisionLayers.Monster
        });
    }

    private void TickBurnDamage(float delta)
    {
        var entities = World.GetEntitiesWith<EffectComponent, HealthComponent>();
        foreach (var entity in entities)
        {
            if (!entity.IsAlive) continue;
            if (entity.Has<DeathPendingComponent>()) continue;

            var effect = entity.Get<EffectComponent>();
            if (!effect.IsBurning) continue;

            effect.BurnTimer -= delta;
            effect.BurnTickTimer -= delta;

            if (effect.BurnTickTimer <= 0)
            {
                effect.BurnTickTimer += 1f; // tick every 1 second

                var health = entity.Get<HealthComponent>();
                health.Hp -= effect.BurnDamagePerTick;
                if (health.Hp < 0) health.Hp = 0;
            }

            if (effect.BurnTimer <= 0)
            {
                effect.IsBurning = false;
                effect.BurnTickTimer = 0;
            }
        }
    }

    private void TickFreezeDuration(float delta)
    {
        var entities = World.GetEntitiesWith<EffectComponent, VelocityComponent>();
        foreach (var entity in entities)
        {
            if (!entity.IsAlive) continue;
            if (entity.Has<DeathPendingComponent>()) continue;

            var effect = entity.Get<EffectComponent>();
            if (!effect.IsFrozen) continue;

            effect.FreezeTimer -= delta;
            if (effect.FreezeTimer <= 0)
            {
                effect.IsFrozen = false;
            }
        }
    }
}

/// <summary>Marker to prevent bounced arrows from bouncing again.</summary>
public class BounceMarkerComponent { }
