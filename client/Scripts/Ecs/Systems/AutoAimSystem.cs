using System;
using Godot;
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

            // Apply upgrades to bow stats each frame
            if (upgrade != null)
            {
                bow.ArrowCount = UpgradeData.GetArrowCount(upgrade.MultiShotLevel);
                bow.SpreadAngle = UpgradeData.GetSpreadAngle(upgrade.MultiShotLevel);
                bow.Cooldown = UpgradeData.GetCooldown(upgrade.AttackSpeedLevel);
                bow.Damage = UpgradeData.GetArrowDamage(upgrade.DamageLevel);
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

            // Fire arrows when ready and has target
            if (bow.CooldownTimer <= 0 && nearestMonster != null)
            {
                bow.CooldownTimer = bow.Cooldown;

                var targetTransform = nearestMonster.Get<TransformComponent>();
                Vector2 direction = (targetTransform.Position - playerTransform.Position).Normalized();

                int pierceCount = upgrade != null ? UpgradeData.GetPierceCount(upgrade.PierceLevel) : 0;

                if (bow.ArrowCount <= 1)
                {
                    SpawnArrow(player, direction, bow, pierceCount);
                }
                else
                {
                    float totalSpread = bow.SpreadAngle;
                    int count = bow.ArrowCount;

                    // Even count: symmetric around center with a minimum gap
                    // e.g. 2 arrows → offsets: -half_gap, +half_gap (no overlap, both near forward)
                    // Odd count:  center arrow at 0°, others spread evenly
                    // MinGap ensures 2 arrows never overlap
                    const float MinGapDeg = 5f;

                    for (int i = 0; i < count; i++)
                    {
                        float offset;
                        if (count % 2 == 1)
                        {
                            // Odd: middle arrow at 0, others evenly spaced
                            offset = (i - (count - 1) / 2.0f) * (totalSpread / Mathf.Max(count - 1, 1));
                        }
                        else
                        {
                            // Even: no arrow at center; place symmetrically around 0
                            // gap = max(totalSpread / count, MinGap) is the spacing between arrows
                            float gap = Mathf.Max(totalSpread / Mathf.Max(count - 1, 1), MinGapDeg);
                            // index from center: -1.5, -0.5, +0.5, +1.5 for count=4
                            offset = (i - (count - 1) / 2.0f) * gap;
                        }
                        float angleRad = Mathf.DegToRad(offset);
                        Vector2 dir = direction.Rotated(angleRad);
                        SpawnArrow(player, dir, bow, pierceCount);
                    }
                }
            }
        }
    }

    private void SpawnArrow(Entity owner, Vector2 direction, BowComponent bow, int pierceCount)
    {
        var arrow = World.CreateEntity();

        arrow.Add(new TransformComponent
        {
            Position = owner.Get<TransformComponent>().Position,
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
            PierceCount = pierceCount
        });

        arrow.Add(new ColliderComponent
        {
            Radius = 5f,
            Layer = CollisionLayers.Arrow,
            Mask = CollisionLayers.Monster
        });
    }
}
