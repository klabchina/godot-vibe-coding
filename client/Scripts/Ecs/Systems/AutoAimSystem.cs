using System;
using Game.Ecs.Core;
using Game.Ecs.Components;
using Game.Data;

namespace Game.Ecs.Systems;

public class AutoAimSystem : GameSystem
{
    public override void Update(float delta)
    {
        var players = World.GetEntitiesWith<PlayerComponent, TransformComponent, BowComponent>();
        var monsters = World.GetEntitiesWith<MonsterComponent, TransformComponent>();

        foreach (var player in players)
        {
            var autoAim = player.Get<AutoAimComponent>();
            if (autoAim == null)
                continue;

            var bow = player.Get<BowComponent>();
            var playerTransform = player.Get<TransformComponent>();
            var upgrade = player.Get<UpgradeComponent>();
            var buff = player.Get<BuffComponent>();

            // Apply upgrades to bow stats each frame
            if (upgrade != null)
            {
                bow.ArrowCount = UpgradeData.GetArrowCount(upgrade.MultiShotLevel);
                bow.SpreadAngle = UpgradeData.GetSpreadAngle(upgrade.MultiShotLevel);
                bow.Cooldown = UpgradeData.GetCooldown(upgrade.AttackSpeedLevel);
                bow.Damage = UpgradeData.GetArrowDamage(upgrade.DamageLevel);
            }

            // Frenzy buff: halve cooldown
            float effectiveCooldown = bow.Cooldown;
            if (buff != null && buff.ActiveTimedBuff == BuffType.Frenzy)
            {
                effectiveCooldown /= PickupData.FrenzyShootMultiplier;
            }

            // Update cooldown
            bow.CooldownTimer -= delta;

            // Find nearest alive monster
            float nearestDist = float.MaxValue;
            Entity nearestMonster = null;

            foreach (var monster in monsters)
            {
                var monsterTransform = monster.Get<TransformComponent>();
                float dist = playerTransform.Position.DistanceTo(monsterTransform.Position);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestMonster = monster;
                }
            }

            autoAim.TargetId = nearestMonster?.Id ?? -1;

            // 行走时禁止射击
            var velocity = player.Get<VelocityComponent>();
            if (velocity != null && velocity.Velocity.LengthSquared() > 0.1f)
                continue;

            // Fire arrows when ready and has target
            if (bow.CooldownTimer <= 0 && nearestMonster != null)
            {
                bow.CooldownTimer = effectiveCooldown;

                var targetTransform = nearestMonster.Get<TransformComponent>();
                Vec2 direction = (targetTransform.Position - playerTransform.Position).Normalized();

                int pierceCount = upgrade != null ? UpgradeData.GetPierceCount(upgrade.PierceLevel) : 0;
                bool hasBounce = upgrade?.HasBounce ?? false;
                bool hasExplosion = upgrade?.HasExplosion ?? false;
                bool hasFreeze = upgrade?.HasFreeze ?? false;
                bool hasBurn = upgrade?.HasBurn ?? false;

                if (bow.ArrowCount <= 3)
                {
                    // No angle offset; offset spawn position left/right perpendicular to forward direction
                    int count = bow.ArrowCount;
                    const float LateralGap = 15f;
                    Vec2 perpendicular = new Vec2(-direction.Y, direction.X); // 90° left of direction

                    for (int i = 0; i < count; i++)
                    {
                        float lateralOffset = (i - (count - 1) / 2.0f) * LateralGap;
                        Vec2 spawnOffset = perpendicular * lateralOffset;
                        SpawnArrow(player, direction, bow, pierceCount, hasBounce, hasExplosion, hasFreeze, hasBurn, spawnOffset);
                    }
                }
                else
                {
                    float totalSpread = bow.SpreadAngle;
                    int count = bow.ArrowCount;

                    const float MinGapDeg = 5f;

                    for (int i = 0; i < count; i++)
                    {
                        float offset;
                        if (count % 2 == 1)
                        {
                            offset = (i - (count - 1) / 2.0f) * (totalSpread / GMath.Max(count - 1, 1));
                        }
                        else
                        {
                            float gap = GMath.Max(totalSpread / GMath.Max(count - 1, 1), MinGapDeg);
                            offset = (i - (count - 1) / 2.0f) * gap;
                        }
                        float angleRad = GMath.DegToRad(offset);
                        Vec2 dir = direction.Rotated(angleRad);
                        SpawnArrow(player, dir, bow, pierceCount, hasBounce, hasExplosion, hasFreeze, hasBurn);
                    }
                }
            }
        }
    }

    private void SpawnArrow(Entity owner, Vec2 direction, BowComponent bow, int pierceCount,
        bool bouncing, bool explosive, bool freezing, bool burning, Vec2 positionOffset = default)
    {
        var arrow = World.CreateEntity();

        arrow.Add(new TransformComponent
        {
            Position = owner.Get<TransformComponent>().Position + positionOffset,
            Rotation = direction.Angle()
        });

        arrow.Add(new VelocityComponent
        {
            Velocity = direction * PlayerData.ArrowSpeed,
            Speed = PlayerData.ArrowSpeed
        });

        arrow.Add(new ArrowComponent
        {
            Damage = bow.Damage,
            OwnerId = owner.Id,
            PierceCount = pierceCount,
            Bouncing = bouncing,
            Explosive = explosive,
            Freezing = freezing,
            Burning = burning
        });

        arrow.Add(new ColliderComponent
        {
            Radius = 5f,
            Layer = CollisionLayers.Arrow,
            Mask = CollisionLayers.Monster
        });
    }
}
