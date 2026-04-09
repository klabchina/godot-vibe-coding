using System;
using Godot;
using Game.Ecs;
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

                if (bow.ArrowCount <= 1)
                {
                    SpawnArrow(player, direction, bow);
                }
                else
                {
                    float totalSpread = bow.SpreadAngle;
                    int count = bow.ArrowCount;

                    for (int i = 0; i < count; i++)
                    {
                        float offset = (i - (count - 1) / 2.0f) * (totalSpread / Mathf.Max(count - 1, 1));
                        float angleRad = Mathf.DegToRad(offset);
                        Vector2 dir = direction.Rotated(angleRad);
                        SpawnArrow(player, dir, bow);
                    }
                }
            }
        }
    }

    private void SpawnArrow(Entity owner, Vector2 direction, BowComponent bow)
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
            PierceCount = 0
        });

        arrow.Add(new ColliderComponent
        {
            Radius = 5f,
            Layer = CollisionLayers.Arrow,
            Mask = CollisionLayers.Monster
        });
    }
}
